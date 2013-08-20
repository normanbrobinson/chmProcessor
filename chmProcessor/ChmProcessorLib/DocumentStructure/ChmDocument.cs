/* 
 * chmProcessor - Word converter to CHM
 * Copyright (C) 2008 Toni Bennasar Obrador
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
 */

using System;
using System.IO;
using mshtml;
using System.Collections.Generic;
using System.Web;
using System.Text;
using WebIndexLib;

namespace ChmProcessorLib.DocumentStructure
{
	/// <summary>
	/// Structured version of a HTML document.
    /// It stores the document itself, and the document sections tree
	/// </summary>
    public class ChmDocument
    {

        /// <summary>
        /// File name where to store the content of the initial part of the document that comes without any
        /// section
        /// </summary>
        public const string INITIALSECTIONFILENAME = "start.htm";

        /// <summary>
        /// File name to store embedded CSS styles generated by MSWord
        /// </summary>
        public const string EMBEDDEDCSSFILENAME = "embeddedstyles.css";

        /// <summary>
        /// Default title for the start of the help content (start of the document out of any section,
        /// entire help content on a single page, etc)
        /// </summary>
        public const string DEFAULTTILE = "Help content";

        /// <summary>
        /// Ultimo nodo insertado en el arbol.
        /// </summary>
        private ChmDocumentNode ultimoInsertado;

        /// <summary>
        /// The original html document
        /// </summary>
        public IHTMLDocument2 IDoc;

        /// <summary>
        /// The root node for the document.
        /// </summary>
        public ChmDocumentNode RootNode;

        /// <summary>
        /// The document index: A plain list of the topics, sorted by its title
        /// </summary>
        public List<ChmDocumentNode> Index;

        /// <summary>
        /// Word saves HTML with a embedded stye tag on the header with the document styles.
        /// This member stores the CSS declarations of this tag. Its null if that tag was not found.
        /// </summary>
        public string EmbeddedStylesTagContent;

        /// <summary>
        /// Constructor
        /// TODO: Remove this constructor
        /// </summary>
        public ChmDocument()
        {
            RootNode = new ChmDocumentNode( null , null , null );
            RootNode.HeaderLevel = 0;
        }

        /// <summary>
        /// Constructor
        /// <param name="iDoc">The document to parse</param>
        /// </summary>
        public ChmDocument(IHTMLDocument2 iDoc) : this()
        {
            IDoc = iDoc;
        }

        private void ListaArchivosGenerados(List<string> lista, ChmDocumentNode nodo) 
        {
            if( !nodo.DestinationFileName.Equals("") && !lista.Contains(nodo.DestinationFileName) )
                lista.Add( nodo.DestinationFileName);
            foreach( ChmDocumentNode hijo in nodo.Children )
                ListaArchivosGenerados( lista , hijo );
        }

        /// <summary>
        /// Obtiene la lista de archivos HTML que se generaran.
        /// </summary>
        /// <returns>Lista de strings con los nombres de los archivos generados.</returns>
        public List<string> ListaArchivosGenerados() 
        {
            List<string> lista = new List<string>();
            ListaArchivosGenerados( lista , this.RootNode );
            return lista;
        }

        /// <summary>
        /// Searches the first section with a given title. 
        /// The comparation is done without letter case.
        /// </summary>
        /// <param name="sectionTitle">The section title to seach</param>
        /// <returns>The first section of the document with that title. null if no section was
        /// found.</returns>
        public ChmDocumentNode SearchBySectionTitle(string sectionTitle)
        {
            return RootNode.SearchBySectionTitle(sectionTitle);
        }

        /// <summary>
        /// Saves the splitted content files of the document to HTML files into a directory.
        /// </summary>
        /// <param name="node">Current node on the recursive search</param>
        /// <param name="savedFiles">The content file names saved</param>
        /// <param name="directoryDstPath">Directory path where the content files will be stored</param>
        /// <param name="decorator">Tool to generate and decorate the HTML content files</param>
        /// <param name="indexer">Tool to index the saved content files. It can be null, if the content
        /// does not need to be indexed.</param>
        private void SaveContentFiles(ChmDocumentNode node, List<string> savedFiles, string directoryDstPath, HtmlPageDecorator decorator, WebIndex indexer)
        {

            string fileName = node.SaveContent(directoryDstPath, decorator, indexer);
            if (fileName != null)
                savedFiles.Add(fileName);

            foreach (ChmDocumentNode child in node.Children)
                SaveContentFiles(child, savedFiles, directoryDstPath, decorator, indexer);
        }

        /// <summary>
        /// Saves the splitted content files of the document to HTML files into a directory.
        /// </summary>
        /// <param name="directoryDstPath">Directory path where the content files will be stored</param>
        /// <param name="decorator">Tool to generate and decorate the HTML content files</param>
        /// <param name="indexer">Tool to index the saved content files. It can be null, if the content
        /// does not need to be indexed.</param>
        /// <returns>The content file names saved</returns>
        public List<string> SaveContentFiles(string directoryDstPath, HtmlPageDecorator decorator, WebIndex indexer)
        {
            // Search nodes with body on the document tree
            List<string> savedFiles = new List<string>();
            foreach (ChmDocumentNode node in RootNode.Children)
                SaveContentFiles(node, savedFiles, directoryDstPath, decorator, indexer);

            // Save the CSS file:
            if (!string.IsNullOrEmpty(EmbeddedStylesTagContent))
            {
                string cssFilePath = Path.Combine( directoryDstPath , EMBEDDEDCSSFILENAME );
                StreamWriter writer = new StreamWriter(cssFilePath);
                writer.Write(EmbeddedStylesTagContent);
                writer.Close();
                savedFiles.Add(EMBEDDEDCSSFILENAME);
            }

            return savedFiles;
        }

        /// <summary>
        /// Makes a recursive search to ghet the first node with content of the document.
        /// If none is found, return nulls.
        /// </summary>
        private ChmDocumentNode FirstNodeWithContentSearch(ChmDocumentNode node)
        {
            if (node.SplittedPartBody != null)
                return node;

            foreach (ChmDocumentNode child in node.Children)
            {
                ChmDocumentNode aux = FirstNodeWithContentSearch(child);
                if (aux != null)
                    return aux;
            }
            return null;
        }

        /// <summary>
        /// Returns the fist node that has HTML content.
        /// If none is found, return null
        /// </summary>
        public ChmDocumentNode FirstNodeWithContent
        {
            get { return FirstNodeWithContentSearch(RootNode); }
        }

        /// <summary>
        /// Returns the inner HTML of the body of the first splitted help content of the document.
        /// If none is found, return nulls
        /// </summary>
        public string FirstSplittedContent
        {
            get {
                ChmDocumentNode firstNode = FirstNodeWithContentSearch(RootNode);
                if (firstNode == null)
                    return null;
                return firstNode.SplittedPartBody.innerHTML.Replace("about:blank", "").Replace("about:", "");
            }
        }

        /// <summary>
        /// Returns true if the parsed document was empty.
        /// </summary>
        public bool IsEmpty
        {
            get { return RootNode.Children.Count == 0; }
        }

    }
}
