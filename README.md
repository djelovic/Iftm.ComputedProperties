# Iftm.ComputedProperties

This project enables _automagic_ firing of [PropertyChanged][1] events. It's perfect for view models.

## NuGet

The binaries from this repository are available as the [Iftm.ComputedProperties][5] NuGet packge.

## Example

Say you have a property A, and a property B that is evaluated as A + 5. To have the [PropertyChanged][1] event fired for both A and B whenever A changes, simply inherit from WithComputedProperties like this:

```C#
using Iftm.ComputedProperties;

class Demo : WithComputedProperties
{
    private int _a; // value of A

    public int A {
        get => _a;
        set => SetProperty(ref _a, value); 
    }

    // Describe our computation for B using an expression:
    private static readonly ComputedProperty<Demo, int> _b =
        Computed((Demo demo) => demo.A + 5);

    // Implement a property that evaluates _b:
    public int B => _b.Eval(this);
}
```
The magic of this is that whenever A changes, the [PropertyChanged][1] event will be fired for both A and B.

This also works for complex properties like a.B.C.D where a, B and C are objects, the property computation is a conditional expressions, etc.

## Lifetime

In case object A has properties that depend on object B, the whenever anybody is subscribed to A's [PropertyChanged][1] event A will also subscribe to B's [PropertyChanged][1]. When A has no more listeners it will disconnect from B.

You can also call [Dispose()][3] on the model above which will disconnect it from all the input events and clear its [PropertyChanged][1] event. Frameworks like [WPF][4] use the weak event pattern to disconnect from events but explicitly disposing the model should yield better performance and provide for better debuggability.

## Performance

The Iftm.ComputedProperties package is ridiculously efficient. Although it tracks property dependencies at runtime (because in case of A.B.C the object A needs to know when B changes so that it can unsubscribe from the old B and subscribe to the new one), the memory usage is miniscule and cache-friendly, and the CPU usage is negligible. If you are binding to these objects in any UI framework like UWP or WPF you will not feel it and your code will definitely be more robust and simpler.

Iftm.ComputedProperties blows frameworks like [ReactiveUI][2] out of the water both in performance and simplicity. Everything is stored inside the single object instead of spawning tens of Observables in order to do simple computations.

[1]: https://docs.microsoft.com/en-us/dotnet/api/system.componentmodel.inotifypropertychanged.propertychanged?view=netframework-4.8

[2]: https://reactiveui.net

[3]: https://docs.microsoft.com/en-us/dotnet/api/system.idisposable.dispose?view=netframework-4.8#System_IDisposable_Dispose

[4]: https://github.com/dotnet/wpf

[5]: https://www.nuget.org/packages/Iftm.ComputedProperties/