# Introduction #

Designing a finite state machine in synthesizable VHDL can be a cumbersome task. Especially when you're faced with control flow things can get confusing and error-prone. The result is a large VHDK `case` statement which is hard to understand and hard to maintain.

Expressing the same behavior using sequential code would be way more intuitive. But unfortunately, VHDL synthesis tools bail out as soon as they discover any `wait` statement inside the code. In contrast, System# enables you to generate synthesizable finite state machines from clocked threads.

Let's start with a simple example: A parallel/serial converter: many external circuits, such as digital-to-analog converters have a serial interface. A hardware design being in control of such a circuit has to split the data word in consecutive bits, and tick them one by one into the device. The interface definition of an appropriate parallel/serial converter might look like this:

```cs

class BitSerializer : Component
{
public In<StdLogic> Clk { private get; set; }
public In<StdLogic> DataReady { private get; set; }
public In<StdLogicVector> ParIn { private get; set; }
public Out<StdLogic> SyncOut { private get; set; }
public Out<StdLogic> SerOut { private get; set; }
}
```

**Note:** You will find a complete version of this example when you open Visual Studio project `Example_SetParBits` which is part of the System# installation.

The converter is clocked by input `Clk`. When there is a valid data word to be serialized at `ParIn`, `DataReady` are set to `'1'` for one clock cycle. To indicate the start of a serial transmission, the converter sets `SyncOut` to `'1'` for one clock cycle and ticks the serial data out of `SerOut`.

The following clocked thread implements the desired behavior:
```cs

[TransformIntoFSM]

private void Serialize()
{
DesignContext.Wait();
do
{
StdLogicVector cap;
SyncOut.Next = '0';

// Capture data port and wait for DataReady

cap = ParIn.Cur;
while (!DataReady.Cur)
{
DesignContext.Wait();
cap = ParIn.Cur;
}

// Start serial transmission
SyncOut.Next = '1';
for (int i = 0; i < _size; i++)
{
SerOut.Next = cap[i];
DesignContext.Wait();
SyncOut.Next = '0';
}
} while (true);
}
```

Did you notice the `[TransformIntoFSM]` attribute at the top? Indeed, this attribute instructs System# not to translate the process literally but to transform it to a finite state machine. Let's have a look at the generated VHDL state machine:

```vhd

process(Clk)
variable local0: std_logic_vector(26 downto 0);
begin
if (rising_edge(Clk)) then
case m_Serialize_State is
when State0 =>
SyncOut <= '0';
local0 := ParIn;
if (op_Implicit(not SyncIn)) then
m_Serialize_State <= State1;
else
SyncOut <= '1';
SerOut <= local0(0);
m_Serialize_State <= State2;
end if;
-- break
when State1 =>
local0 := ParIn;
if (op_Implicit(not SyncIn)) then
m_Serialize_State <= State1;
else
SyncOut <= '1';
SerOut <= local0(0);
m_Serialize_State <= State2;
end if;
-- break
when State2 =>
-- states 3..27 omitted for brevity
when State28 =>
SyncOut <= '0';
SyncOut <= '0';
local0 := ParIn;
if (op_Implicit(not SyncIn)) then
m_Serialize_State <= State29;
else
SyncOut <= '1';
SerOut <= local0(0);
m_Serialize_State <= State2;
end if;
-- break
when State29 =>
local0 := ParIn;
if (op_Implicit(not SyncIn)) then
m_Serialize_State <= State29;
else
SyncOut <= '1';
SerOut <= local0(0);
m_Serialize_State <= State2;
end if;
-- break
end case;
end if;
end process;
```
The implementation was completely unrolled along the loop variable `i` and poured into a big FSM.

# How to use the feature #

At first: Only a _clocked thread_ can be translated to an FSM. All you have to do:
  * Import the right namespace:
```cs

using SystemSharp.Analysis.M2M;
```
  * Declare attribute `[TransformIntoFSM]` at the top of each clocked thread you which to be transformed:
```cs

[TransformIntoFSM]
private void MyProcess() { ... }
```

# Current limitations #

  1. The _very first_ statement of the clocked process to be transformed _must_ be a call to `DesignContext.Wait()`. This is because System# semantics prescribe a clocked thread to begin its execution along with the start of simulation. So any code prior to the first wait statement actually describes reset behavior. We currently do not support that. However, this will be an issue for future releases.
  1. Every loop is completely unrolled. This often leads to more efficient designs because the loop iteration space is implicitly encoded in the state and not in a separate variable for which synthesis must infer a binary counter. However, unrolling is only viable if the loop does not take to much iterations (let's say at most 100). As we know that this assumption can be prohibitive for some designs, we're working on the possibility to let the designer control which local variables should be unrolled and which ones should be kept.
  1. If you look at the generated state machines, you'll find that they often have replicated states. For an engineer it's easy to see that some states could be merged. But for the transformation engine it's not that self-evident (another issue for future releases...).
  1. Compared to hand-coded FSMs, the generated ones consume a lot of registers. This is due to the sequential specification style: Once a variable or signal is not assigned in each and every branch between two `Wait()` statements, the VHDL synthesis tool has to infer a register. This leads us to a further future improvement: The transformation engine could try to infer missing assignments.