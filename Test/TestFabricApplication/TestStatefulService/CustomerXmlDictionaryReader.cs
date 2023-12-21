﻿//  ----------------------------------------------------------------------------------
//  Copyright Microsoft Corporation
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//  http://www.apache.org/licenses/LICENSE-2.0
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//  ----------------------------------------------------------------------------------


using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace TestStatefulService
{
    internal class CustomerXmlDictionaryReader : XmlDictionaryReader
    {
        XmlDictionaryReader innerReader;
        string targetNamespace;
        public CustomerXmlDictionaryReader(XmlDictionaryReader innerReader, string targetNamespace)
        {
            this.innerReader = innerReader;
            this.targetNamespace = targetNamespace;

        }

        public override int AttributeCount => this.innerReader.AttributeCount;

        public override string BaseURI => this.innerReader.BaseURI;

        public override int Depth => this.innerReader.Depth;

        public override bool EOF => this.innerReader.EOF;

        public override bool IsEmptyElement => this.innerReader.IsEmptyElement;

        public override string LocalName => this.innerReader.LocalName;

        public override string NamespaceURI
        {
            get
            {
                // Alter the old namespace
                UriBuilder builder = new UriBuilder(innerReader.NamespaceURI);
                var segments = builder.Uri.Segments;
                if (!segments.LastOrDefault().EndsWith("/") && segments.LastOrDefault() != this.targetNamespace)
                {
                    segments[segments.Length - 1] = this.targetNamespace;
                    builder.Path = String.Join("", segments);
                    return builder.Uri.ToString();
                }

                return innerReader.NamespaceURI;
            }
        }

        public override XmlNameTable NameTable => this.innerReader.NameTable;

        public override XmlNodeType NodeType => this.innerReader.NodeType;

        public override string Prefix => this.innerReader.Prefix;

        public override ReadState ReadState => this.innerReader.ReadState;

        public override string Value => this.innerReader.Value;

        public override string GetAttribute(int i)
        {
            return this.innerReader.GetAttribute(i);
        }

        public override string GetAttribute(string name)
        {
            return this.innerReader.GetAttribute(name);
        }

        public override string GetAttribute(string name, string namespaceURI)
        {
            return this.innerReader.GetAttribute(name, namespaceURI);
        }

        public override string LookupNamespace(string prefix)
        {
            return this.innerReader.LookupNamespace(prefix);
        }

        public override bool MoveToAttribute(string name)
        {
            return this.innerReader.MoveToAttribute(name);
        }

        public override bool MoveToAttribute(string name, string ns)
        {
            return this.innerReader.MoveToAttribute(name, ns);
        }

        public override bool MoveToElement()
        {
            return this.innerReader.MoveToElement();
        }

        public override bool MoveToFirstAttribute()
        {
            return this.innerReader.MoveToFirstAttribute();
        }

        public override bool MoveToNextAttribute()
        {
            return this.innerReader.MoveToNextAttribute();
        }

        public override bool Read()
        {
            return this.innerReader.Read();
        }

        public override bool ReadAttributeValue()
        {
            return this.innerReader.ReadAttributeValue();
        }

        public override void ResolveEntity()
        {
            this.innerReader.ResolveEntity();
        }
    }
}
