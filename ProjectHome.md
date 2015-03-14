# Overview #

System# is a .NET library which is intended for the description of real-time embedded systems. It comes with a built-in simulator kernel and a code transformation engine which converts a design into synthesizable VHDL.

System#'s main focus is currently the development of FPGA designs. System# not only supports register-transfer-level (RTL) descriptions whose translation to VHDL is straightforward. It is also capable of converting clocked threads with wait statements to a synthesizable VHDL state machine. Furthermore, System# introduces synthesizable transaction-level modeling features.

From a technological point of view, System# uses reflection and assembly code (CIL) decompilation to reconstruct an abstract syntax tree (AST) from the system design. The AST conforms to SysDOM - a document object model for describing component-based reactive systems. An unparsing stage converts the AST to VHDL. The decompilation process can be instrumented in various ways by attribute-based programming. Furthermore, transformations of the AST itself are possible. This enables us to implement the advanced features, such as converting clocked threads to finite state machines.

# Getting started #

  * Obtain [NUGET package manager](https://www.nuget.org/)
  * Inside Visual Studio, create a new C# project, e.g. a console application
  * Open the package manager console
  * Type:
```
Install-Package systemsharp
```
  * Have a look at the [project wiki](http://code.google.com/p/systemsharp/wiki/Index)
  * See the [code documentation](http://systemsharp.googlecode.com/svn/doc/trunk/html/index.html)