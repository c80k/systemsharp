----------------------------------------------------------------------------
-- Copyright (c) 1996, Ben Cohen.   All rights reserved.
-- This model can be used in conjunction with the Kluwer Academic books
-- "VHDL Coding Styles and Methodologies", ISBN: 0-7923-9598-0
-- "VHDL Amswers to Frequently Asked Questions", ISBN 0-7923-9791-6
-- Web page:   http://members.aol.com/vhdlcohen/vhdl
-- email: vhdlcohen@aol.com
--
-- This source file for the image  package
-- may be used and distributed without restriction provided
-- that this copyright statement is not removed from the file
-- and that any derivative work contains this copyright notice.
------------------------------------------------------------------------------- 
library IEEE; 
  use IEEE.Std_Logic_1164.all;
  use IEEE.Std_Logic_TextIO.all;  
  use IEEE.Std_Logic_Arith.all;

library Std;
  use STD.TextIO.all;

package Image_Pkg is
  function Image(In_Image : Time) return String;
  function Image(In_Image : Bit) return String;
  function Image(In_Image : Bit_Vector) return String;
  function Image(In_Image : Integer) return String;
  function Image(In_Image : Real) return String;
  function Image(In_Image : Std_uLogic) return String;
  function Image(In_Image : Std_uLogic_Vector) return String;
  function Image(In_Image : Std_Logic_Vector) return String;
  function Image(In_Image : Signed) return String;
  function Image(In_Image : UnSigned) return String;

end Image_Pkg;

package body Image_Pkg is
  function Image(In_Image : Time) return String is
    variable L : Line;  -- access type
    variable W : String(1 to 25) := (others => ' '); 
       -- Long enough to hold a time string
  begin
    -- the WRITE procedure creates an object with "NEW".
    -- L is passed as an output of the procedure.
    Std.TextIO.WRITE(L, in_image);
    -- Copy L.all onto W
    W(L.all'range) := L.all;
    Deallocate(L);
    return W;
  end Image;

  function Image(In_Image : Bit) return String is
    variable L : Line;  -- access type
    variable W : String(1 to 3) := (others => ' ');  
  begin
    Std.TextIO.WRITE(L, in_image);
    W(L.all'range) := L.all;
    Deallocate(L);
    return W;
  end Image;

  function Image(In_Image : Bit_Vector) return String is
    variable L : Line;  -- access type
    variable W : String(1 to In_Image'length) := (others => ' ');  
  begin
    Std.TextIO.WRITE(L, in_image);
    W(L.all'range) := L.all;
    Deallocate(L);
    return W;
  end Image;

  function Image(In_Image : Integer) return String is
    variable L : Line;  -- access type
    variable W : String(1 to 32) := (others => ' ');  
     -- Long enough to hold a time string
  begin
    Std.TextIO.WRITE(L, in_image);
    W(L.all'range) := L.all;
    Deallocate(L);
    return W;
  end Image;

  function Image(In_Image : Real) return String is
    variable L : Line;  -- access type
    variable W : String(1 to 32) := (others => ' ');  
      -- Long enough to hold a time string
  begin
    Std.TextIO.WRITE(L, in_image);
    W(L.all'range) := L.all;
    Deallocate(L);
    return W;
  end Image;

  function Image(In_Image : Std_uLogic) return String is
    variable L : Line;  -- access type
    variable W : String(1 to 3) := (others => ' ');  
  begin
    IEEE.Std_Logic_Textio.WRITE(L, in_image);
    W(L.all'range) := L.all;
    Deallocate(L);
    return W;
  end Image;

  function Image(In_Image : Std_uLogic_Vector) return String is
    variable L : Line;  -- access type
    variable W : String(1 to In_Image'length) := (others => ' ');  
  begin
    IEEE.Std_Logic_Textio.WRITE(L, in_image);
    W(L.all'range) := L.all;
    Deallocate(L);
    return W;
  end Image;

  function Image(In_Image : Std_Logic_Vector) return String is
    variable L : Line;  -- access type
    variable W : String(1 to In_Image'length) := (others => ' ');  
  begin
     IEEE.Std_Logic_TextIO.WRITE(L, In_Image);
     W(L.all'range) := L.all;
     Deallocate(L);
     return W;
  end Image;

  function Image(In_Image : Signed) return String is 
  begin 
    return Image(Std_Logic_Vector(In_Image));
  end Image;

  function Image(In_Image : UnSigned) return String is
  begin 
    return Image(Std_Logic_Vector(In_Image));
  end Image;

end Image_Pkg;
