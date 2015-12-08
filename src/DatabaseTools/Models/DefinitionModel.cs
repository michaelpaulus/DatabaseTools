﻿
using DatabaseTools.Extensions;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Xml.Linq;

namespace DatabaseTools
{
    namespace Models
    {
        public class DefinitionModel : DatabaseObjectModel
        {

            #region Properties

            public string DefinitionName { get; set; }
            public string Definition { get; set; }
            public string XType { get; set; }

            public string Type
            {
                get
                {
                    switch (this.XType)
                    {
                        case "P":
                            return "PROCEDURE";
                        case "V":
                            return "VIEW";
                        case "FN":
                        case "IF":
                            return "FUNCTION";
                    }
                    return string.Empty;
                }
            }

            #endregion

            #region Methods

            public override void AppendDropScript(System.Text.StringBuilder sb, string quoteCharacterStart, string quoteCharacterEnd)
            {
                if (sb.Length > 0)
                {
                    sb.AppendLine();
                }

                sb.AppendLine(string.Format("-- {0}", this.DefinitionName));
                sb.AppendLine(string.Format("IF EXISTS (SELECT 1 FROM sysobjects WHERE sysobjects.name = '{0}')", this.DefinitionName));
                sb.AppendLine("\t" + string.Format("DROP {0} {1}", this.Type, this.DefinitionName));
                sb.AppendLine("GO");
            }

            public override void AppendCreateScript(System.Text.StringBuilder sb, string quoteCharacterStart, string quoteCharacterEnd)
            {
                if (sb.Length > 0)
                {
                    sb.AppendLine();
                }

                sb.AppendLine(string.Format("-- {0}", this.DefinitionName));
                sb.AppendLine(string.Format("IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE sysobjects.name = '{0}')", this.DefinitionName));

                string strPattern = string.Format("(CREATE\\s*{0}\\s*[\\[]?)([\\[]?dbo[\\.]?[\\]]?[\\.]?[\\[]?)?({1})([\\]]?)", this.Type, this.DefinitionName);

                string strDefinitionReplacement = string.Format("CREATE {0} dbo.{1}", this.Type, this.DefinitionName);

                this.Definition = System.Text.RegularExpressions.Regex.Replace(this.Definition, strPattern, strDefinitionReplacement, System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Multiline);

                sb.AppendLine(string.Format("EXEC sp_executesql @statement = N'{0}'", this.Definition.Replace("'", "''")));
                sb.AppendLine("GO");
            }

            #endregion

        }
    }


}