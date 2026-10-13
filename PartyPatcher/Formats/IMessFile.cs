using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PartyPatcher.Formats
{
    public interface IMessFile
    {
        void Replace(int groupIdx, int messageIdx, string message);
        void AddMessage(int groupIdx, string message);
        int AddGroup();
        void Save(string filePath, uint version = 4, Encoding encoding = null);
    }
}
