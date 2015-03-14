# Welcome to the System# wiki #

## Installation ##

### Platform ###

System# is being developed using Visual Studio and the .NET framework. That's why we currently stick to the Windows environment. Although System# should theoretically run on MONO, platform independence is currently not the main focus of the project. However, you are encouraged to give us feedback on platform-specific issues.

### Prerequisites ###

Before you install System#, please make sure your machine meet the following prerequisites:

  * **Visual Studio 2012**

  * **.NET Framework 4.5** (usually installed along with Visual Studio 2012)

  * NuGet Package Manager

  * System# supports Xilinx IP Cores and generates a Xilinx ISE project along with the VHDL sources. **Xilinx ISE Design Suite 11.x, 12.x** or **13.x** is recommended (no matter what edition). **14.x** should also work (it will offer you to convert the project to the newer version).

### How to build ###

If you want to rebuild System# from scratch, please follow these instructions:

  * Recommended, but not mandatory: Install **Code Contracts**. Try http://research.microsoft.com/en-us/projects/contracts/.

  * Download the **boost library, v1.46** (legacy version is mandatory if you don't like to fix tons of compiler errors) from http://www.boost.org/ and extract the contents of the `boost` folder to `SystemSharp\GraphAlgorithmLib\boost` (path is relative to repository trunk).

  * Open solution `AllInOne`.

  * Open project settings of `GraphAlgorithmLib_VC2012`. Remove preprocessor definition `INCLUDE_DIGITAL_SIGNATURE` for all build configurations. _Background_: This option is for digitally signing the assembly. It requires a strong key file, which is not included in the repository. Since the intent of a digital key is to uniquely identify the author of an assembly, it will never be included in the repository. If you require a digital signature for some reason, feel free to create your own `systemsharp.pfx`.

  * Open project settings of `SystemSharp`. Select tab "Signing" and uncheck option "Sign the assembly". Again, this is because you don't have the keyfile.

  * Build the solution. Usually, NuGet will detect that there are some dependencies on external libraries and should normally download and install them automatically. If this does not work for some reasons (or you're getting errors because of missing assemblies), here is an overview:

| **Project** | **Dependencies** | **NuGet package manager console command** |
|:------------|:-----------------|:------------------------------------------|
| SystemSharp | Microsoft Reactive Extensions | `Install-Package Rx-Main` |
| XilinxSupportLib | SQLite | `Install-Package System.Data.SQLite` |


### Download and Setup ###

The easiest way to install System#: Create a new C# project inside Visual Studio, open the NuGet package manager console and type:
```
Install-Package systemsharp
```

# Getting Started #

The first thing you may want to do is to browse the examples. These will give you a good starting point to see how things work in System#. If you  download the current repository trunk, there will be some examples included.

Most of the examples are composed of two execution phases: The first phase is a delta-cycle simulation. During the simulation, you can observe the example design action by its console outputs (sorry, not wave viewer yet). So far, nothing special. The second phase constitutes the actual magic: The design is being analyzed by the System# reflection engine and then transformed into VHDL. The examples hard-code the output directory to `.\hdl_output`, so you will find the generated sources in

`[PATH_TO_SYSTEM#]\SystemSharp\Example_[EXAMPLE_NAME]\bin\[CONFIGURATION]\hdl_output`

in which `[CONFIGURATION]` is your current build configuration (either `Debug` or `Release`).