﻿using System.Linq;
using System.Xml.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Shapes;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;

namespace DatabaseTools.Configuration.Preferences
{
    public class UserSettings : PropertyChangedObject
    {

        #region Properties

        public string CreateScriptsPath { get; set; }
        public string ScriptsPath { get; set; }
        public string TargetConnectionString { get; set; }
        public string SourceConnectionString { get; set; }

        private System.Collections.Specialized.StringCollection _mergeTableLists;
        public System.Collections.Specialized.StringCollection MergeTableLists
        {
            get
            {
                if (this._mergeTableLists == null)
                {
                    this._mergeTableLists = new System.Collections.Specialized.StringCollection();
                }
                return this._mergeTableLists;
            }
            set
            {
                this._mergeTableLists = value;
            }
        }

        public string TargetMssql { get; set; }
        public string SourceMssql { get; set; }

        public string TargetDSN { get; set; }
        public string SourceDSN { get; set; }

        public string TargetOLE { get; set; }
        public string SourceOLE { get; set; }

        public string TargetMySql { get; set; }
        public string SourceMySql { get; set; }

        public string TargetAccessOLE { get; set; }
        public string SourceAccessOLE { get; set; }

        public string MainWindowPlacement { get; set; }

        #endregion

        

        protected override void OnPropertyChanged(string propertyName)
        {
            base.OnPropertyChanged(propertyName);
        }

    }

}