# Introduction #

In order to get System#-generated files simulated with Mentor ModelSim it is necessary to compile the `ieee_proposed` library (we'll need `fixed_pkg` and `float_pkg`) which is distributed with Xilinx ISE. Forthermore, we need to add the compiled project as a global library mapping to ModelSim. Please note that ModelSim contains some `fixed_pkg` and `float_pkg` as well - however, they are not compatible with System#.

This procedure was tested with Mentor ModelSim SE 6.5e and Xilinx ISE 13.2. Different tool versions should behave similarly. You are kindly requested to tell us your experiences.


# Details #

  1. Start ModelSim and create a new project. Name the project `floatfixlib` and enter `ieee_proposed` as default library name. You can choose any project location which is convenient for you. We recommend to use a folder name similar to `C:\Xilinx\13.2\ISE_DS\ISE\vhdl\mti_se\6.5e\nt64\ieee_proposed`.
  1. Add the following files to the project: `fixed_float_types_c.vhd`, `fixed_pkg_c.vhd`, `float_pkg_c.vhd`. You'll find them in a folder silimar to `C:\Xilinx\13.2\ISE_DS\ISE\vhdl\src\ieee_proposed`.
  1. From the ModelSim menu bar, select _Compile_, _Compile Order..._. In the dialog which pops up, click _Auto Generate_.
  1. Wait for the compilation, then close the project.
  1. In the ModelSim _Library_ window, right click and choose _New_, _Library..._ from the context menu.
  1. Select _a map to an existing library_. For the library name, enter `ieee_proposed`. For _Library maps to_, browse to the location of the project you created. Click _Ok_.
  1. Open `modelsim.ini` (you'll find it in the Modelsim installation folder) in a text editor and add the following line to the `Library` section: `ieee_proposed = C:/Xilinx/13.2/ISE_DS/ISE/vhdl/mti_se/6.5e/nt64/ieee_proposed` (again, modify the path according to your installation and mind the forward slashes which are required even on a Windows machine).
  1. That's all folks.