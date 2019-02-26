using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpeckleGSA
{
    [AttributeUsage(AttributeTargets.Class)]
    public class GSAObject : Attribute
    {
        private string gsaKeyword;
        private string stream;
        private bool analysisLayer;
        private bool designLayer;
        private Type[] readPrerequisite;
        private Type[] writePrerequisite;

        public GSAObject(string gsaKeyword, string stream, bool analysisLayer, bool designLayer, Type[] readPrerequisite, Type[] writePrerequisite)
        {
            this.gsaKeyword = gsaKeyword;
            this.stream = stream;
            this.analysisLayer = analysisLayer;
            this.designLayer = designLayer;
            this.readPrerequisite = readPrerequisite;
            this.writePrerequisite = writePrerequisite;
        }

        public virtual string GSAKeyword
        {
            get { return gsaKeyword; }
        }

        public virtual string Stream
        {
            get { return stream; }
        }

        public virtual bool AnalysisLayer
        {
            get { return analysisLayer; }
        }

        public virtual bool DesignLayer
        {
            get { return designLayer; }
        }

        public virtual Type[] ReadPrerequisite
        {
            get { return readPrerequisite; }
        }

        public virtual Type[] WritePrerequisite
        {
            get { return writePrerequisite; }
        }
    }
}
