using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace InnovaCodeEditor.DTO
{
    public class CodeRequest
    {
        public List<CodeFile> Files { get; set; }
        public string Input { get; set; }
    }

    public class CodeFile
    {
        public string FileName { get; set; }
        public string Code { get; set; }
    }
}