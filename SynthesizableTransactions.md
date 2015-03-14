# Introduction #

Transaction-level modeling (TLM) is a technique which strengthens the separation of computation and communication in system-level design. Communication to a certain peer, e.g. a bus or IP core is broken down to so-called transactions. Each transaction describes a well-defined activity at some communication end-point. E.g. a bus model typically yields two transactions _Read_ and _Write_. You already guessed it: _Read_ models reading a data word from the bus wheras _Write_ models writing a word to it. Depending on the bus architecture there may be more complex transactions, such as _BurstRead_ or _BurstWrite_. From a programatical point of view, transactions are implemented by procedures or methods. These contain all the required handshake logic to perform the transaction, e.g. setting a `request` signal, waiting for `acknowledge` and accounting for wait cycles. Therefore, transactions greatly simplify the development of testbenches and system mockups: Instead of driving each bit separately, you write down an intuitive sequence of transactions.

TLM was successfully applied to the domain of verification and design-space exploration. However, it is usually restricted to the simulation domain. That is, you can simulate a TLM, but you usually can't synthesize it. That's where System# transactions come into play: These _are_ designed to be suitable for synthesizable HDL generation. The downside: System# TLM is clearly not as powerful as SystemC TLM 2.0. We restrict ourselves to a simple use case: Transactions are there to feed some IP core with data and to fetch the result out of it. Nothing ambitious. Don't expect any fancy bus or interface synthesis. But if your goal is to integrate a memory, a divider core and some I/O, System# offers an elegant solution to you.

# Functional units #

TLM always involves two parties: One that initiates a transaction (the transactor) and one that accepts it (the transaction target). Think of a divider core: The transactor sets divisor and dividend, waits for the transaction to complete and returns the quotient. The divider core is the transaction target, performing the actual computation.

## Transaction site definition ##

We'll begin with the transaction target. To enable transaction support for a component, it must be derived from a special class: `SystemSharp.Components.FU.FunctionalUnit`. A functional unit exposes one or more _transaction sites_. A transaction site is like a virtual port which is able to receive transactions. E.g. a dual-ported memory has two ports which allow for independent memory accesses. Consequently, it exposes two transaction sites. Of course, the simple divider gets along with a single transaction site. A transaction site defines the set of possible transactions. To define a transaction site, derive an interface from `SystemSharp.Components.Transactions.ITransactionSite`. This interface is defined as follows:
```cs

public interface ITransactionSite
{
FunctionalUnit Host { [StaticEvaluation] get; }
object Group { [StaticEvaluation] get; }

[StaticEvaluation] IEnumerable<TAVerb> DoNothing();
}
```
The base interface defines the following properties and methods:
  * `Host` depicts the functional unit which will be the target of any transaction.
  * `Group` denotes a group identifier which is used to distinguish multiple transactions sites uniquely.
  * `DoNothing()` is the so-called neutral transaction. Any transaction site must implement this special transaction. It is executed whenever there is no other transaction to be performed.
The derived interface should define a method for each transaction which is supported by the target. The following code snippet shows an exemplary memory interface definition:
```cs

public interface IMemoryInterface: ITransactionSite
{
[StaticEvaluation] IEnumerable<TAVerb> Read(
ISignalSource<StdLogicVector> addr,
ISignalSink<StdLogicVector> data);

[StaticEvaluation] IEnumerable<TAVerb> Write(
ISignalSource<StdLogicVector> addr,
ISignalSource<StdLogicVector> data);
}
```
Yes, the syntax is a little bit strange. Stick to the following rules, and you're on the safe side:
  * Each method is tagged with a `[StaticEvaluation]` attribute.
  * Each method returns an `IEnumerable<TAVerb>`. We'll discuss that later.
  * Input arguments are passed as `ISignalSource<Type>` (`Type` is the argument type)
  * Output arguments are passed as `ISignalSink<Type>` (`Type` is the argument type)

## Transaction site implementation ##

The next step is to implement the previously defined transaction site interface. You can either add the implementation to the functional unit itself or you can choose to provide the implementation within a separate class.

Each transaction is expected to return an `IEnumerable<TAVerb>`. The idea behind is that each transaction is modeled by a sequence (`IEnumerable`) of _transaction verbs_ (`TAVerb`). A transaction verb specifies the transactor's behavior during a single clock step. To complete a transaction, the transaction verbs within the sequence get executed one-by-one until there are no elements left. This kind of modeling has two implications:
  * Transactions are expected to be synchronous to a single clock signal.
  * Transactions are a straight sequence of clock-synchronous actions. There is no control flow.

Let's proceed with the implementation of a transaction. `FunctionUnit` defines a protected member `Verb` which is intended to create a single transaction verb. This member comes in two flavors:
```cs

protected TAVerb Verb(ETVMode mode, Action action);
protected TAVerb Verb(ETVMode mode, Action action, IProcess during);
```
The first argument specifies the mode of the transaction verb. It can be either `ETVMode.Locked` or `ETVMode.Shared`.
  * `ETVMode.Locked` means that the transaction verb needs exclusive access to the transaction target. Another transaction may overlap only if its current transaction verb has the mode `ETVMode.Shared`. `ETVMode.Locked` should be specified if the transaction target does not support pipelining or during the initiation interval if it does support pipelining.
  * `ETVMode.Shared` means that other transactions may overlap the current transaction verb.
The second argument expects a delegate which describes the actions to be performed on the transaction target. This includes setting control bits and transferring arguments in and out of the component interface. You can think of it as of a triggered process. It is executed exactly once when the rising (or falling) edge event which belongs to the current transaction verb is triggered.

The optional third argument can be used to specify a so-called _during action_. The during action can be thought as of a combinatorial process. It is executed whenever a signal inside its sensitivity list changes - but only during the clock interval which belong to the current transaction verb.

The following code snippet shows an exemplary implementation of the `Read` transaction of a Xilinx block memory. I decided to implement `IMemoryInterface` within a separate class. `_mem` refers to the actual `BlockMem` class.
```cs

public IEnumerable<TAVerb> Read(ISignalSource<StdLogicVector> addr, ISignalSink<StdLogicVector> data)
{
if (_useEn)
{
yield return _mem.Verb(ETVMode.Locked, () =>
{
_en.Next = '1';
_we.Next = "0";
},
_addrIn.Drive(addr)
.Par(_dataIn.Drive(SignalSource.Create(StdLogicVector._0s(_dataOut.Size())))));

}
else
{
yield return _mem.Verb(ETVMode.Locked, () =>
{
_we.Next = "0";
},
_addrIn.Drive(addr)
.Par(_dataIn.Drive(SignalSource.Create(StdLogicVector._0s(_dataOut.Size())))));

}
if (data.Comb != null)
{
yield return _mem.Verb(ETVMode.Shared,
() => { },
data.Comb.Connect(_dataOut.AsSignalSource())

.Par(_addrIn.Drive(addr))

.Par(_dataIn.Drive(SignalSource.Create(StdLogicVector._0s(_dataOut.Size())))));

}
else if (data.Sync != null)
{
yield return _mem.Verb(ETVMode.Shared,
() => { data.Sync.Write(_dataOut.Cur); },

_addrIn.Drive(addr)

.Par(_dataIn.Drive(SignalSource.Create(StdLogicVector._0s(_dataOut.Size())))));

}
}
```

# Transactors #

**Note:** Have a look at Visual Studio project `Example_RTL_TLM` which is part of the System# installation.

To enable a component for initiating transactions, it must be derived from `SystemSharp.Components.Transactions.TransactingComponent`. This class provides a member called `AddTAProcess` which is declared as follows:
```cs

protected void AddTAProcess(Action func, params TATarget[] targets)
```
It is used to declare a process which is able to initiate transactions. The first argument is the process, following by one or more transaction targets. It is important that each transaction target resides within the same clock domain. Otherwise, `AddTAProcess` will throw an exception. Another member will help you with the definition of transaction targets: `TransactOn`. It is declared as follows:
```cs

protected TATarget TransactOn(ITransactionSite site)
```
You put in a transaction site, and `TransactOn` will turn it into a transaction target.

Inside the transactor process, call `Issue` to initiate a transaction:
```cs

protected void Issue(object group, IEnumerable<TAVerb> ta)
```
The `group` argument uniquely identifies the transaction site. `ta` is the return value you got from calling your desired transaction method. Remember that the transaction method expects arguments of type `ISignalSource<Type>` and `ISignalSink<Type>`? So the last piece of the jigsaw is how to connect these argument to your internal signals and variables. The following table will help you:
| **Objective** | **Syntax** |
|:--------------|:-----------|
| Take a constant `value` as transaction input argument | SignalSource.Create(value) |
| Take signal `sig` as transaction input argument | `sig.AsSignalSource()` |
| Take signal `sig` as synchronous transaction output argument | `sig.AsSyncSink()` |
| Take signal `sig` as combinatorial transaction output argument | `sig.AsCombSink()` |

# Translating transacting components to VHDL #

For a transactor process to be translatable to VHDL, it must be tagged with the `[TransformIntoFSM]` attribute.