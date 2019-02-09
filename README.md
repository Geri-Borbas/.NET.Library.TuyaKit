# `Tuya` Kit
> Control [**Tuya**](https://en.tuya.com/) smart home devices


A library to control Tuya smart home devices via local TCP connection.

```C#
// Once you setup a device...
socket = new Woox_R4026()
{
    IP = "192.168.0.100",
    port = 6668,
    protocolVersion = "3.1",
    name = "Lounge Light Socket",
    devId = "<DEV_ID>",
    localKey = "<LOCAL_KEY>"
};

// ...you can simply set states via device wrapper.
socket.TurnOn();
```

![Woox ](https://images-na.ssl-images-amazon.com/images/I/51p06M8jJHL._SY355_.jpg)


## Credits

Based on [Max Isom](https://maxisom.me/)'s reverse engineered [**TuyAPI**](https://github.com/codetheweb/tuyapi) library (and corresponding [TuyaCore](https://github.com/Marcus-L/m4rcus.TuyaCore) .NET port by [Marcus Lum](https://m4rc.us/)).


## Tuya services

Tuya distributed devices are mostly controlled with an [ESP8266 WiFi module](https://www.espressif.com/en/products/hardware/esp8266ex/overview) with a specific firmware (see [Module Overview](https://docs.tuya.com/en/hardware/WiFi-module/wifi-e1s-module.html)).

By default you can control the devices via a Tuya specific app, like [Tuya Smart](https://itunes.apple.com/us/app/tuyasmart/id1034649547?mt=8), [Smart Life](https://itunes.apple.com/us/app/smart-life-smart-living/id1115101477?mt=8), [Jinvoo Smart](https://itunes.apple.com/us/app/jinvoo-smart/id1182632835?mt=8), [Lohas Smart](https://itunes.apple.com/us/app/lohas-smart/id1375829753?mt=8) or [Woox Home](https://itunes.apple.com/us/app/woox-home/id1436052873?mt=8) to name a few. Under the hood they are all various derivatives of the white labeled [Tuya Smart Cloud Service](https://docs.tuya.com/en/overview/index.html) (brands have separate containers for device data though).

Once you have registered your device with one of the apps, it will have a `localKey` assigned upon pairing. From then on the app encrypts device requests with that key.

This library just does the same. Once you have the corresponding device data **it orchestrates the encryption and the communication protocol** via local TCP connection.


## Getting device data

After you registered you device in one of the consumer apps mentioned above, you can find device information details in there somewhere. It tells you the **`devId`** (Smart Life app calls it *Virtual ID*) and the *Mac Address*. Having the Mac Address you can look up the **Local IP Address** of the device in the DHCP Client List of your WiFi Router. **Port** number and **Protocol Version** is set to `6668` and `3.1` by default.

Getting the **`localKey`** can be tricky. Luckily [Max Isom](https://maxisom.me/) created a tool [`tuya-cli`](https://github.com/TuyaAPI/cli) to extract device data from the network traffic. See [**Linking a Tuya Device**](https://github.com/codetheweb/tuyapi/blob/master/docs/SETUP.md) for a detailed breakdown.


## Device control schema

To obtain the `dps` schema (the device properties you can manage) I simply inspected the console output of the [Android **Smart Life** app](https://play.google.com/store/apps/details?id=com.tuya.smartlife&hl=en) using [`adb logcat`](https://developer.android.com/studio/command-line/logcat). It gives you a pretty detailed log when navigating to the *Device Information* view. For a [Woox R4026 Smart Plug](http://www.wooxhome.com/r4026/) it shows this schema:

```JSON
{
  "initialProps": {
    "devInfo": {

      ...

      "dps": {
        "1": true,
        "9": 0
      },

      ...

      "schema": {
        "1": {
          "type": "obj",
          "name": "开关1",
          "mode": "rw",
          "code": "switch_1",
          "id": "1",
          "schemaType": "bool",
          "iconname": "icon-dp_power2",
          "property": "{\"type\":\"bool\"}"
        },
        "9": {
          "type": "obj",
          "name": "开关1倒计时",
          "mode": "rw",
          "code": "countdown_1",
          "id": "9",
          "schemaType": "value",
          "iconname": "icon-dp_time2",
          "property": "{\"max\":86400,\"min\":0,\"scale\":0,\"step\":1,\"type\":\"value\",\"unit\":\"s\"}"
        }
      }
      
      ...

    }
  }
}
```

It shows a `bool` switch on `["dps"]["1"]` and a countdown value (seconds) on `["dps"]["9"]`.


## Implementing a device

After you created a `Device` instance, you can send any JSON data to it using a `Request` object.

```C#
// Get device properties.
JObject response = await new Request().SendJSONObjectForCommandToDevice(
    new Dictionary<string, object>
    {
        ["gwId"] = this.gwId,
        ["devId"] = this.devId
    },
    Request.Command.GetStatus,
    this);
```

A [Woox R4026 Smart Plug](http://www.wooxhome.com/r4026/) responds with a status like below:
```JSON
{
    "devId":"58205000840d8e46ebb0",
    "dps":
    {
        "1" : true,
        "9" : 0
    }
}
```

It gives you a status report according the very same control schema obtained above. To cut boilerplate, it is wrapped into a simple `Get()` method `Device` class that gives you back only the `dps` data you care about.

```C#
// Get device properties.
Dictionary<string, object> dps = await Get();
```

```JSON
{
    "1" : true,
    "9" : 0
}
```

To set `dps` you can use `Device.Set()`.

```C#
// Set device properties.
await Set(
    new Dictionary<string, object>
    {
        ["1"] = false,
        ["2"] = 0
    }
);
```

Once you have a specific device, you can wrap up `dps` all communication into a `Device` subclass (see `Woox_R4026.cs` for more).

```C#
...
public async void TurnOff()
{
    await Set(
        new Dictionary<string, object>
        {
            ["1"] = false,
            ["2"] = 0
        }
    );	
}

public async void TurnOn()
{
    await Set(
        new Dictionary<string, object>
        {
            ["1"] = true,
            ["2"] = 0
        }
    );	
}
...
```

After that you can use pretty much without any boilerplate.

```C#
socket.TurnOn();
```


## Next up

Will probably implement retry attempts, also I'm planning to create the library for iOS.

Furthermore, would be great if you guys could contribute with various `Device` implementations. I saw that there is a myriad of manufacturers out there licensing Tuya technologies. Let me just highligt some of the brands I encountered.

> Cotify, Ushawn, Elegant Choise, Cxy, Zenic, Sonew, Venoro, Innens, Oittm, Lixada, Woox


## License

> Licensed under the [MIT license](http://en.wikipedia.org/wiki/MIT_License).