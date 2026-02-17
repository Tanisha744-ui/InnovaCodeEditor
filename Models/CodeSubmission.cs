using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace InnovaCodeEditor.Models
{
    public class CodeSubmission
    {
        public List<CodeFile> Files { get; set; } = new List<CodeFile>();
    }

    public class CodeFile
    {
        public string FileName { get; set; }
        public string Code { get; set; }
    }
}