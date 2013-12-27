/**
 * Copyright 2011-2013 Christian Köllner
 * 
 * This file is part of System#.
 *
 * System# is free software: you can redistribute it and/or modify it under 
 * the terms of the GNU Lesser General Public License (LGPL) as published 
 * by the Free Software Foundation, either version 3 of the License, or (at 
 * your option) any later version.
 *
 * System# is distributed in the hope that it will be useful, but WITHOUT ANY
 * WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS 
 * FOR A PARTICULAR PURPOSE. See the GNU General Public License for more 
 * details.
 *
 * You should have received a copy of the GNU General Public License along 
 * with System#. If not, see http://www.gnu.org/licenses/lgpl.html.
 * */


#include "stdafx.h"

#include <utility>
#include <string>
#include <iostream>
#include <deque>
#include <cassert>

// If you get errors about missing include files here, don't worry. You need the boost library.
// Go to www.boost.org, download the boost 1.46 library and extract the "boost" folder to
// SystemSharp/GraphAlgorithLib/boost
// (folder is relative to trunk)
// Some notes on the required version: This library was originally developed using boost version 1.46.
// As I tried the more recent version 1.54 I got tons of errors which I did't find straightforward to solve.
// Feel free fix to this issue... :-)
#include "boost/graph/adjacency_list.hpp"
#include "boost/graph/max_cardinality_matching.hpp"
#include "boost/graph/strong_components.hpp"
#include "boost/graph/topological_sort.hpp"

using namespace boost;

// Suppress warning "this class has a finalizer '...' but no destructor '...'".
// In our case this is safe, since the only unmanaged resource freed in the destructors is heap memory.
// As we rely on GC to release managed memory, the finalizers will care about the unmanaged memory.
#pragma warning( disable : 4461 )

#include "GraphAlgorithmLib.h"
