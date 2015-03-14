# Introduction #

This page describes the steps needed to incorporate System# in a Visual Studio project.


# Details #

  * Create a new C# Application project. It is up to you whether you choose a Windows Forms application, WPF application or console application. If in doubt, create a console application.
![http://systemsharp.googlecode.com/files/screenshot_NewCSApp.png](http://systemsharp.googlecode.com/files/screenshot_NewCSApp.png)
  * In the _Solution Explorer_ right-click _References_ and choose _Add Reference..._
![http://systemsharp.googlecode.com/files/screenshot_AddReference.png](http://systemsharp.googlecode.com/files/screenshot_AddReference.png)
  * Select the _.NET_ tab and left-click _Component Name_ to get an alphabetical sorting of all .NET assemblies in your system. Now add the following four components:
    * **GraphAlgorithmLib**
> > > ![http://systemsharp.googlecode.com/files/screenshot_AddRefGraphAlgorithmLib.png](http://systemsharp.googlecode.com/files/screenshot_AddRefGraphAlgorithmLib.png)
    * **Oyster.IntX**
> > > ![http://systemsharp.googlecode.com/files/screenshot_AddRefOysterIntX.png](http://systemsharp.googlecode.com/files/screenshot_AddRefOysterIntX.png)
    * **SystemSharp**
> > > ![http://systemsharp.googlecode.com/files/screenshot_AddRefSystemSharp.png](http://systemsharp.googlecode.com/files/screenshot_AddRefSystemSharp.png)
    * **XilinxSupportLib**
> > > ![http://systemsharp.googlecode.com/files/screenshot_AddRefXilinxSupportLib.png](http://systemsharp.googlecode.com/files/screenshot_AddRefXilinxSupportLib.png)
  * Add the following import directives at the beginning of each source file where you want to use System#:
```cs

using SystemSharp.Components;
using SystemSharp.Components.Std;
using SystemSharp.DataTypes;
using SystemSharp.Synthesis;
using SystemSharp.Synthesis.VHDLGen;
using SystemSharp.Interop.Xilinx;
using SystemSharp.Interop.Xilinx.IPCores;
```
  * You're ready now to start off your own design.