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


#pragma once

using namespace System;
using namespace System::Collections::Generic;

///<summary>Contains data structures for representing directed and undirected graphs and some fundamental graph algorithms, 
///such as determining maximum matchings, strongly connected components and topological sortings.</summary>
///<remarks>This library is essentially a .NET wrapper around boost.</remarks>
namespace GraphAlgorithms {

	typedef adjacency_list<vecS, vecS, undirectedS> graph_t;
	typedef graph_traits<graph_t>::vertex_descriptor vertex_t;
	typedef std::vector<vertex_t> vertex_vec_t;

	typedef adjacency_list<vecS, vecS, bidirectionalS> digraph_t;
	typedef graph_traits<digraph_t>::vertex_descriptor divertex_t;
	typedef std::vector<divertex_t> divertex_vec_t;

	ref class Graph;
	ref class Digraph;

	///<summary>Represents a maximum matching inside an undirected graph.</summary>
	///<remarks>This class cannot be constructed by the user. Instead it is returned as a result of GetMaximumMatching of class Graph.</remarks>
	public ref class Matching
	{
		!Matching()
		{
			delete m_pMate;
		}

	internal:
		///<summary>Constructs an instance based on an unmanaged pointer to a graph.</summary>
		Matching(graph_t* pg)
		{
			m_pMate = new mate_t(num_vertices(*pg));
			m_result =
				checked_edmonds_maximum_cardinality_matching(*pg, &(*m_pMate)[0]);
		}

	private:
		typedef vertex_vec_t mate_t;

	public:
		///<summary>Returns the mate of a given vertex identifier v inside the matching. If the vertex is unmatched, -1 is returned.</summary>
		property int default[int]
		{
			int get(int v)
			{
				if (v < 0 || v >= (int)m_pMate->size())
					return -1;
				if ((*m_pMate)[v] == graph_traits<graph_t>::null_vertex())
					return -1;
				else
					return (*m_pMate)[v];
			}
		}

		///<summary>Returns true iff matching is of maximum cardinality</summary>
		property bool IsMaximumCardinality				
		{
			bool get()
			{
				return m_result;
			}
		}

	private:
		std::vector<graph_traits<graph_t>::vertex_descriptor>*
					m_pMate;
		bool		m_result;
	};

	///<summary>Represents the strongly connected components of a directed graph.</summary>
	///<remarks>This class cannot be constructed by the user. Instead, it is returned as a result of method GetStrongComponents of class Digraph.</remarks>
	public ref class StrongComponents
	{	
		!StrongComponents()
		{
			delete m_pComponent;
			delete m_pDiscoverTime;
			delete m_pColor;
			delete m_pRoot;
		}

	internal:
		///<summary>Constructs an instance based on an unmanaged pointer to a digraph</summary>
		StrongComponents(digraph_t* pg)
		{
			int nv = num_vertices(*pg);
			m_pComponent = new ivec_t(nv);
			m_pDiscoverTime = new ivec_t(nv);
			m_pColor = new cvec_t(nv);
			m_pRoot = new divertex_vec_t(nv);
			m_num = strong_components(*pg, &((*m_pComponent)[0]), 
							  root_map(&((*m_pRoot)[0])).
                              color_map(&((*m_pColor)[0])).
                              discover_time_map(&((*m_pDiscoverTime)[0])));
		}

	private:
		typedef std::vector<int> ivec_t;
		typedef std::vector<default_color_type> cvec_t;

	public:
		///<summary>Number of strong components</summary>
		property int NumComponents
		{
			int get()
			{
				return m_num;
			}
		}

		///<summary>Returns a 0-based identifier of the component where vertex identifier v belongs to.</summary>
		property int default[int]
		{
			int get(int v)
			{
				if (v < 0 || v >= (int)m_pComponent->size())
					return -1;
				else
					return (*m_pComponent)[v];
			}
		}

	private:
		int					m_num;
		ivec_t*				m_pComponent;
		ivec_t*				m_pDiscoverTime;
		cvec_t*				m_pColor;
		divertex_vec_t*		m_pRoot;
	};

	///<summary>Represents a topological sorting of a directed graph</summary>
	///<remarks>This class cannot be constructed by the user. It is returned as a result of the GetTopologicalSorting method of Digraph.</remarks>
	public ref class TopologicalSorting
	{
		!TopologicalSorting()
		{
			delete m_pOrder;
		}

	internal:
		///<summary>Constructs an instance based on an unmanaged pointer to a digraph.</summary>
		TopologicalSorting(digraph_t* pg)
		{
			int nv = num_vertices(*pg);
			m_pOrder = new std::deque<int>(nv);
			topological_sort(*pg, 
				std::front_inserter(*m_pOrder),
				vertex_index_map(identity_property_map()));
		}

	public:
		///<summary>Number of vertices</summary>
		property int NumVertices
		{
			int get()
			{
				return m_pOrder->size();
			}
		}

		///<summary>Returns the vertex identifier at 0-based position idx, implied by the sorting.</summary>
		property int default[int]
		{
			int get(int idx)
			{
				if (idx < 0 || idx >= (int)m_pOrder->size())
					return -1;
				else
					return (*m_pOrder)[idx];
			}
		}

	private:
		std::deque<int>*		m_pOrder;
	};

	///<summary>An undirected graph. Vertices are represented by integer values.</summary>
	public ref class Graph
	{
		!Graph()
		{
			delete m_pGraph;
		}

	public:
		///<summary>Constructs an undirected graph with a given number of vertices.</summary>
		Graph(int numVertices)
		{
			m_pGraph = new graph_t(numVertices);
		}

		///<summary>Inserts an edge between two vertices</summary>
		/// <param name="v1">Source vertex</param>
		/// <param name="v2">Target vertex</param>
		void AddEdge(int v1, int v2)
		{
			add_edge(v1, v2, *m_pGraph);
		}

		///<summary>Determines a maximum matching of the graph.</summary>
		///<returns>A data structure for querying the matching result</returns>
		Matching^ GetMaximumMatching()
		{
			Matching^ match = gcnew Matching(m_pGraph);
			return match;
		}

	private:
		graph_t*	m_pGraph;		
	};

	///<summary>A directed graph. Vertices are represented by integer values.</summary>
	public ref class Digraph
	{
		!Digraph()
		{
			delete m_pGraph;
		}

	public:
		///<summary>Constructs an empty directed graph (vertices/edges can be added afterwards).</summary>
		Digraph()
		{
			m_pGraph = new digraph_t();
		}

		///<summary>Constructs a directed graph with a given number of vertices.</summary>
		Digraph(int numVertices)
		{
			m_pGraph = new digraph_t(numVertices);
		}

		///<summary>Current number of vertices</summary>
		property int NumVertices
		{
			int get()
			{
				return num_vertices(*m_pGraph);
			}
		}

		///<summary>Current number of edges</summary>
		property int NumEdges
		{
			int get()
			{
				return num_edges(*m_pGraph);
			}
		}

		///<summary>Adds a new vertex the graph</summary>
		///<returns>Identifier of that vertex</returns>
		int AddNode()
		{
			return add_vertex(*m_pGraph);
		}

		///<summary>Inserts an edge between two vertices</summary>
		/// <param name="v1">Source vertex</param>
		/// <param name="v2">Target vertex</param>
		void AddEdge(int v1, int v2)
		{
			add_edge(v1, v2, *m_pGraph);
		}

		///<summary>Given a vertex, returns the number of outgoing edges</summary>
		/// <param name="v">Vertex identifier</param>
		int GetOutDegree(int v)
		{
			return out_degree(v, *m_pGraph);
		}

		///<summary>Given a vertex, returns the number of incoming edges</summary>
		/// <param name="v">Vertex identifier</param>
		int GetInDegree(int v)
		{
			return in_degree(v, *m_pGraph);
		}

		///<summary>Given a vertex, returns a list of adjacent vertices, connected by at least one outgoing edge.</summary>
		/// <param name="v">Vertex identifier</param>
		List<int>^ GetOutSet(int v)
		{
			List<int>^ result = gcnew List<int>;
			graph_traits<digraph_t>::out_edge_iterator vi, vi_end;
			for (boost::tie(vi,vi_end) = out_edges(v, *m_pGraph); vi != vi_end; ++vi)
			{
				result->Add(target(*vi, *m_pGraph));
			}
			return result;
		}

		///<summary>Given a vertex, returns a list of adjacent vertices, connected by at least one incoming edge.</summary>
		/// <param name="v">Vertex identifier</param>
		List<int>^ GetInSet(int v)
		{
			List<int>^ result = gcnew List<int>;
			graph_traits<digraph_t>::in_edge_iterator vi, vi_end;
			for (boost::tie(vi,vi_end) = in_edges(v, *m_pGraph); vi != vi_end; ++vi)
			{
				result->Add(source(*vi, *m_pGraph));
			}
			return result;
		}

		///<summary>Determines the strongly connected components of the graph.</summary>
		///<returns>A data structure for querying the strongly connected components</returns>
		StrongComponents^ GetStrongComponents()
		{
			StrongComponents^ sc = gcnew StrongComponents(m_pGraph);
			return sc;
		}

		///<summary>Determines a topological sorting of the graph.</summary>
		///<returns>A data structure for querying the topological sorting</returns>
		TopologicalSorting^ GetTopologicalSorting()
		{
			TopologicalSorting^ ts = gcnew TopologicalSorting(m_pGraph);
			return ts;
		}

	private:
		digraph_t*	m_pGraph;		
	};
}
