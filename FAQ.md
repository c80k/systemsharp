#Frequently asked questions

# Frequently Asked Questions #

**What's new/different in System# compared to "_insert your SLDL here_"?**

  * Unlike most other SLDLs (system-level design languages), System# is based on C# and the .NET framework.

  * System# is not just restricted to simulation - it is a combined simulation/synthesis framework, featuring the generation of synthesizable VHDL from models. It is not only an embedded domain-specific language - it is also a tool.

  * Unlike most other synthesis tools, System# is open source.

  * To our best knowledge, we present the first open source tool which features generation of synthesizable VHDL from C#.

**What about [ESys.NET](http://sourceforge.net/projects/esyssim/)?**

  * ESys.NET is a system-level modeling and simulation environment and has therefore a similar objective. However, it is currently restricted to simulation and does not feature code generation. System# is not related to this project in any way and did not derive any sources from it. System# is a completely unrelated, independent project.

**Is System# a high level synthesis tool?**

  * System# is currently extended towards high level synthesis (HLS). However, this is still a big construction site. Furthermore, System# is not intended as a pure HLS tool. Instead, it is a framework which enables modeling and synthesis at different levels of abstraction. As a system developer, you are always in control which synthesis algorithm should be selected for which part of your system model. So if you need HLS, you have to specify which part of your code should be subject to it.

**System# only supports VHDL generation. Nevertheless, it is presented as a system-level modeling framework and therefore should also support software modeling and code generation. How does that fit together?**

  * You are completely right, there is currently a strong bias towards FPGA design which is our primary application. But conceptually, System# could be perfectly extended towards HW/SW co-design modeling and software code (e.g. C/C++) generation. The only reason why this is not yet done is due to limited developer capacity. If you're interested in making a contribution, please let us know.

**Does System# also support Altera/Actel/"_insert your favorite FPGA vendor here_" toolchains?**

  * No, System# creates only Xilinx ISE projects and features a limited set of Xilinx IP cores. If you're not using IP cores, the generated VHDL sources themselves are of course device-independent. You can manually integrate them into your favored simulation/synthesis tool. We don't have and won't have the resources to implement support for other vendors. Of course, you can change that by making a contribution. :-)

**How to contribute?**

  * The simplest thing: Try System# and send us your feedback. Did you find a bug? Did you miss any feature? We're highly interested in improving the overall implementation quality!

  * Use System# for your FPGA design tell us about your application. Your design will be a helpful example for other developers.

  * Improve toolchain support in System#: Add more IP cores to the Xilinx library or even create new support libraries for other FPGA vendors.

  * Write a code generator for Verilog/SystemVerilog/....


  * Got your own idea? Let us know!