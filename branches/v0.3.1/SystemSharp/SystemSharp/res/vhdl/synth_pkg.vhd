-- "systemsharp.synth_pkg" package contains support functions which are
-- needed to synthesize System#-generated VHDL.
--

library ieee;
use ieee.std_logic_1164.all;
use ieee.std_logic_unsigned.all;
use ieee.numeric_std.all;
use ieee.math_real.all;

package synth_pkg is

  function to_std_logic(L: BOOLEAN) 
    return std_ulogic;

  function to_std_logic_vector(sl: std_logic) 
    return std_logic_vector;

end package synth_pkg;

package body synth_pkg is

  function to_std_logic(L: BOOLEAN) return std_ulogic is
  begin
    if L then
      return('1');
    else
      return('0');
    end if;
  end function to_std_Logic;

  function to_std_logic_vector(sl: std_logic) return std_logic_vector is
    variable slv: std_logic_vector(0 downto 0);
  begin
    slv(0) := sl;
	return slv;
  end function to_std_logic_vector;

end package body synth_pkg;
