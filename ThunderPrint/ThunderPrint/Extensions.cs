using Ical.Net;
using Ical.Net.DataTypes;

namespace ThunderPrint
{
    public static class Extensions
    {
        public static Event GetEvent(this Occurrence occurrence)
        {
            return (Event) occurrence.Source;
        }
    }
}
