using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProcessPriorityControl.Cmd
{
    enum Priority
    {
        Idle = 0,
        BelowNormal = 1,
        Normal = 2,
        AboveNormal = 3,
        High = 4,
        HighWithScript = 5,
        Ignore = -1,
        ConditionalIdle = -2
    }
}
