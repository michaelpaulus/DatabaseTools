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
		public class ForeignKeyDetailModel
		{

			public string Column {get; set;}
			public string ReferencedColumn {get; set;}

		}
	}


}