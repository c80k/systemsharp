/**
 * Copyright 2011 Christian Köllner
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

using System.Diagnostics.Contracts;
using System.Xml;

namespace SystemSharp.Reporting
{
    /// <summary>
    /// Provides functionality for creating simple HTML documents.
    /// </summary>
    public class HTMLReport
    {
        private XmlWriter _out;

        /// <summary>
        /// Constructs an instance.
        /// </summary>
        /// <param name="fileName">full path to the HTML file to create.</param>
        public HTMLReport(string fileName)
        {
            Contract.Requires(fileName != null);

            _out = XmlWriter.Create(fileName, new XmlWriterSettings()
                {
                    Indent = true
                });
        }

        /// <summary>
        /// Begins an HTML document.
        /// </summary>
        /// <param name="title">title of document</param>
        public void BeginDocument(string title)
        {
            _out.WriteStartElement("html");
            _out.WriteStartElement("header");
            _out.WriteElementString("title", title);
            _out.WriteEndElement();
            _out.WriteStartElement("body");
        }

        /// <summary>
        /// Ends the HTML document.
        /// </summary>
        public void EndDocument()
        {
            _out.WriteEndElement();
            _out.WriteEndElement();
        }

        /// <summary>
        /// Adds a header.
        /// </summary>
        /// <param name="level">hierarchy level, 0 is topmost</param>
        /// <param name="title">header title</param>
        public void AddSection(int level, string title)
        {
            _out.WriteElementString("h" + level, title);
        }

        /// <summary>
        /// Adds text to the document.
        /// </summary>
        /// <param name="text">text to add</param>
        public void AddText(string text)
        {
            _out.WriteString(text);
        }

        /// <summary>
        /// Begins a new paragraph.
        /// </summary>
        public void BeginParagraph()
        {
            _out.WriteStartElement("p");
        }

        /// <summary>
        /// Ends current paragraph.
        /// </summary>
        public void EndParagraph()
        {
            _out.WriteEndElement();
        }

        /// <summary>
        /// Begins a new table.
        /// </summary>
        /// <param name="border">border width</param>
        /// <param name="captions">column captions</param>
        public void BeginTable(int border, params string[] captions)
        {
            _out.WriteStartElement("table");
            _out.WriteAttributeString("border", border.ToString());
            _out.WriteStartElement("tr");
            foreach (string cap in captions)
                _out.WriteElementString("th", cap);
            _out.WriteEndElement();
        }

        /// <summary>
        /// Begins a new table.
        /// </summary>
        /// <param name="captions">column captions</param>
        public void BeginTable(params string[] captions)
        {
            _out.WriteStartElement("table");
            _out.WriteStartElement("tr");
            foreach (string cap in captions)
                _out.WriteElementString("th", cap);
            _out.WriteEndElement();
        }

        /// <summary>
        /// Begins a new row inside current table.
        /// </summary>
        /// <param name="cols">column texts</param>
        public void AddRow(params string[] cols)
        {
            _out.WriteStartElement("tr");
            foreach (string col in cols)
                _out.WriteElementString("td", col);
            _out.WriteEndElement();
        }

        /// <summary>
        /// Ends current table.
        /// </summary>
        public void EndTable()
        {
            _out.WriteEndElement();
        }

        /// <summary>
        /// Begins a new bullet list.
        /// </summary>
        public void BeginBulletList()
        {
            _out.WriteStartElement("ul");
        }

        /// <summary>
        /// Ends current bullet list.
        /// </summary>
        public void EndBulletList()
        {
            _out.WriteEndElement();
        }

        /// <summary>
        /// Begins a new enumeration.
        /// </summary>
        public void BeginEnumeration()
        {
            _out.WriteStartElement("ol");
        }

        /// <summary>
        /// Ends current enumeration.
        /// </summary>
        public void EndEnumeration()
        {
            _out.WriteEndElement();
        }

        /// <summary>
        /// Adds an item to the current bullet list or enumeration.
        /// </summary>
        /// <param name="text">item text to add</param>
        public void AddListItem(string text)
        {
            _out.WriteElementString("li", text);
        }

        /// <summary>
        /// Begins a new item inside the current bullet list or enumeration.
        /// </summary>
        public void BeginListItem()
        {
            _out.WriteStartElement("li");
        }

        /// <summary>
        /// Ends the current list item.
        /// </summary>
        public void EndListItem()
        {
            _out.WriteEndElement();
        }

        /// <summary>
        /// Closes the document.
        /// </summary>
        public void Close()
        {
            _out.Close();
        }
    }
}
