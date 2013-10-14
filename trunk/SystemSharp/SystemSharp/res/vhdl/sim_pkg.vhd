-- --------------------------------------------------------------------
-- "systemsharp.sim_pkg" package contains support functions which are
-- needed to simulate System#-generated VHDL.
--

library ieee;
use ieee.std_logic_1164.all;
use ieee.std_logic_unsigned.all;
use ieee.numeric_std.all;
use ieee.math_real.all;
library ieee_proposed;
use ieee_proposed.float_pkg.all;

package sim_pkg is
  function sin (x: float)
    return float;

  function cos (x: float)
    return float;

end package sim_pkg;

package body sim_pkg is

  function sin (x: float)
    return float is
  begin
    return to_float(sin(to_real(x)), x);
  end function sin;

  function cos (x: float)
    return float is
  begin
    return to_float(cos(to_real(x)), x);
  end function cos;

end package body sim_pkg;
