﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OcerraOdoo.Models
{
    public class ImportResult
    {
        public int NewItems { get; set; }
        public int UpdatedItems { get; set; }
        public string Message { get; set; }

        public bool HasErrors { get; set; }
    }
}
