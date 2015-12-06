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
        public class ColumnModel : Model
        {
            #region Properties

            private string _tableName;
            public string TableName
            {
                get
                {
                    return _tableName;
                }
                set
                {
                    _tableName = value;
                    this.OnPropertyChanged("TableName");
                }
            }

            private string _columnName;
            public string ColumnName
            {
                get
                {
                    return _columnName;
                }
                set
                {
                    _columnName = value;
                    this.OnPropertyChanged("ColumnName");
                }
            }

            private int _columnID;
            public int ColumnID
            {
                get
                {
                    return _columnID;
                }
                set
                {
                    if (!(int.Equals(this._columnID, value)))
                    {
                        _columnID = value;
                        this.OnPropertyChanged("ColumnID");
                    }
                }
            }

            public bool IsPrimaryKey { get; set; }

            private int _precision;
            public int Precision
            {
                get
                {
                    return this._precision;
                }
                set
                {
                    if (!(int.Equals(this._precision, value)))
                    {
                        this._precision = value;
                        this.OnPropertyChanged("Precision");
                    }
                }
            }

            private int _scale;
            public int Scale
            {
                get
                {
                    return _scale;
                }
                set
                {
                    if (!(int.Equals(this._scale, value)))
                    {
                        this._scale = value;
                        this.OnPropertyChanged("Scale");
                    }
                }
            }

            private bool _isNullable;
            public bool IsNullable
            {
                get
                {
                    return _isNullable;
                }
                set
                {
                    _isNullable = value;
                    this.OnPropertyChanged("IsNullable");
                }
            }


            private bool _isIdentity;
            public bool IsIdentity
            {
                get
                {
                    return _isIdentity;
                }
                set
                {
                    _isIdentity = value;
                    this.OnPropertyChanged("IsIdentity");
                }
            }

            private bool _isComputed;
            public bool IsComputed
            {
                get
                {
                    return _isComputed;
                }
                set
                {
                    _isComputed = value;
                    this.OnPropertyChanged("IsComputed");
                }
            }

            private string _computedDefinition;
            public string ComputedDefinition
            {
                get
                {
                    return _computedDefinition;
                }
                set
                {
                    if (value != null)
                    {
                        if (value.Contains("CONVERT") && value.Contains("(0)))"))
                        {
                            value = value.Replace("(0)))", "0))");
                        }
                    }
                    _computedDefinition = value;
                    this.OnPropertyChanged("ComputedDefinition");
                }
            }

            private string _columnType;
            public string ColumnType
            {
                get
                {
                    return _columnType;
                }
                set
                {
                    if (!(string.Equals(this._columnType, value)))
                    {
                        this._columnType = value;
                        this.OnPropertyChanged("ColumnType");
                    }
                }
            }

            public System.Data.DbType DbType
            {
                get
                {
                    return Processes.Database.GetDBType(this.ColumnType);
                }
            }

            #endregion

            #region Methods

            public string ToString(string quoteCharacter)
            {
                string strDataType = this.ColumnType;

                switch (strDataType.ToUpper())
                {
                    case "DECIMAL":
                    case "NUMERIC":
                        strDataType = string.Format("{0}({1}, {2})", this.ColumnType, this.Precision, this.Scale);
                        break;
                    case "BINARY":
                    case "VARBINARY":
                    case "VARCHAR":
                    case "CHAR":
                    case "NVARCHAR":
                    case "NCHAR":
                        string strPrecision = this.Precision.ToString();
                        if (this.Precision == -1 || this.Precision == Int32.MaxValue)
                        {
                            strPrecision = "MAX";
                        }
                        strDataType = string.Format("{0}({1})", this.ColumnType, strPrecision);
                        break;
                }

                if (this.IsComputed)
                {
                    strDataType = "AS " + this.ComputedDefinition;
                }

                string strIdentity = string.Empty;

                if (this.IsIdentity)
                {
                    strIdentity = " IDENTITY(1,1)";
                }

                string strNull = "NULL";

                if (!this.IsNullable)
                {
                    strNull = "NOT " + strNull;
                }

                if (this.IsComputed)
                {
                    strNull = string.Empty;
                }

                return string.Format("{4}{0}{4} {1}{2} {3}", this.ColumnName, strDataType, strIdentity, strNull, quoteCharacter).Trim();
            }

            public override string ToString()
            {
                return this.ToString("");
            }

            #endregion

        }
    }


}