# Introduction #

There are situations in which you need _n_ instances of some component with _n_ being a tunable parameter (e.g. think of an _n_-tap shift register). The same is true for signals: In some cases you actually need an array of signals. It seems tempting to use C# arrays to solve the problem. However, this would not only lead to subtle problems in your design specification, but - worse - would hinder the System# analysis engine from "understanding" your design right. Instead, System# provides several datatyps for handling arrays of components and signals the "right" way.

Have a look at the example project `Example_Arbiter` which is part of the System# installation. It will provide you with some exemplary code.

# Arrays of components #

A component array is modled by class `ComponentCollection` (namespace: `SystemSharp.Components`). Be sure you declare the `ComponentCollection` as a (typically private) field inside the hosting component. The constructor of `ComponentCollection` takes a single argument of type `IEnumerable<Component>`. This argument depicts the actual sequence of component instances you want to be put inside the `ComponentCollection`.

# Array-typed signals #

If you're familiar with VHDL, you'll have to rethink things a little bit: The VHDL way of declaring an array of signals is actually to declare a signal of an array-valued datatype. If multiple processes drive portions of the same signal, it is up to the VHDL compiler to figure out which process drives which portion. System# does not distinguish signal drivers by portion. Either a process drives a signal or not, regardless of whether it drives only a single bit inside the signal or the complete one. However, the `SystemSharp.Components` namespace defines a special class which models an array of signals: `Signal1D<Type>`. Internally, it stores an array of `Signal<Type>`, so the generic type parameter `Type` depicts the type of a single signal value element. Because there is a dedicated `Signal<>` instance for each element the analysis engine is able to distinguish a separate set of drivers for each `Signal1D<>` element.
The `Signal1D<>` constructor comes in two flavors:
  * `Signal1D(IEnumerable<Signal<Type>>)` takes a sequence of already constructed `Signal<>` instances as input.
  * `Signal1D(int dim, Signal1D<Type>.CreateFunc creator)` takes the number of desired signals as first argument and a delegate `creator` as second argument. `creator` is called `dim` times, once for each signal element. It is expected to return a signal instance for each call. This allows for a nice inline-construction of `Signal1D` instances. E.g.
```cs

private Signal1D<StdLogic> _sigvec = new Signal1D<StdLogic>(count, i => new SLSignal());
```
constructs an array of `StdLogic`-valued signals.

**Note**: `Signal1D<StdLogic>` is _not_ the same as `SLVSignal`. Whereas the first models an array of `StdLogic`-valued signals,
the latter defines a single signal of type `StdLogicVector`.

The value of a `Signal1D<>` instance can be accessed in two ways. Compound access treats the signal instance as if it would be a single signal of type `Signal<Type[]>`. Therefore, the `InitialValue`, `Cur`, `Pre` and `Next` properties of a `Signal1D<Type>` all expect an array `Type[]`. Element and slice access is possible by indexer methods. E.g. to manipulate a single bit of a `Signal1D<StdLogic>` instance you write:
```cs

/* assume sig is an instance of Signal1D<StdLogic> */

sig[index].Next = '0';
```

# Array-typed ports #

System# provides special interfaces for the declaration of array-typed ports: `XIn`, `XOut` and `XInOut`. Let's face it: Their syntax is currently rather convoluted due to the generic type parameters they need. This is definitely a point we'll be working on for future releases. The following table will give you the correct syntax to declare an array-typed input, output or bidirectional port which is compatible with `Signal1D<Type>`:

| **Port direction** | **Port property type** |
|:-------------------|:-----------------------|
| input | `XIn<Type[], InOut<Type>>` |
| output | `XOut<Type[], InOut<Type>>` |
| bidirectional | `XInOut<Type[], InOut<Type>>` |