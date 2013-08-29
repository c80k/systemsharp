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
    public class HTMLReport
    {
        private XmlWriter _out;

        public HTMLReport(string fileName)
        {
            Contract.Requires(fileName != null);

            _out = XmlWriter.Create(fileName, new XmlWriterSettings()
                {
                    Indent = true
                });
        }

        public void BeginDocument(string title)
        {
            _out.WriteStartElement("html");
            _out.WriteStartElement("header");
            _out.WriteElementString("title", title);
            _out.WriteEndElement();
            _out.WriteStartElement("body");
        }

        public void EndDocument()
        {
            _out.WriteEndElement();
            _out.WriteEndElement();
        }

        public void AddSection(int level, string title)
        {
            _out.WriteElementString("h" + level, title);
        }

        public void AddText(string text)
        {
            _out.WriteString(text);
        }

        public void BeginParagraph()
        {
            _out.WriteStartElement("p");
        }

        public void EndParagraph()
        {
            _out.WriteEndElement();
        }

        public void BeginTable(int border, params string[] captions)
        {
            _out.WriteStartElement("table");
            _out.WriteAttributeString("border", border.ToString());
            _out.WriteStartElement("tr");
            foreach (string cap in captions)
                _out.WriteElementString("th", cap);
            _out.WriteEndElement();
        }

        public void BeginTable(params string[] captions)
        {
            _out.WriteStartElement("table");
            _out.WriteStartElement("tr");
            foreach (string cap in captions)
                _out.WriteElementString("th", cap);
            _out.WriteEndElement();
        }

        public void AddRow(params string[] cols)
        {
            _out.WriteStartElement("tr");
            foreach (string col in cols)
                _out.WriteElementString("td", col);
            _out.WriteEndElement();
        }

        public void EndTable()
        {
            _out.WriteEndElement();
        }

        public void BeginBulletList()
        {
            _out.WriteStartElement("ul");
        }

        public void EndBulletList()
        {
            _out.WriteEndElement();
        }

        public void BeginEnumeration()
        {
            _out.WriteStartElement("ol");
        }

        public void EndEnumeration()
        {
            _out.WriteEndElement();
        }

        public void AddListItem(string text)
        {
            _out.WriteElementString("li", text);
        }

        public void BeginListItem()
        {
            _out.WriteStartElement("li");
        }

        public void EndListItem()
        {
            _out.WriteEndElement();
        }

        public void Close()
        {
            _out.Close();
        }
    }
}
