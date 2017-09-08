using System;

namespace ThunderPrint
{
    [Flags]
    public enum ItemFlags : short
    {
        Private = 1,
        HasAttendees = 2,
        HasProperties = 4,
        AllDay = 8,
        HasRecurrence = 16,
        HasExceptions = 32,
        HasAttachments = 64,
        HasRelations = 128,
        HasAlarms = 256,
        RecurrenceIdAllDay = 512
    }
}