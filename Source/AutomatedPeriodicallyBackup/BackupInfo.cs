using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutomatedPeriodicallyBackup
{
    class BackupInfo
    {
        public string Filename { get; set; }
        public TimeSpan Age { get; set; }
    }
}
