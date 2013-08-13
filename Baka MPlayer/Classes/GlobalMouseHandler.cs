﻿// http://stackoverflow.com/questions/2063974/how-do-i-capture-the-mouse-mouse-move-event-in-my-winform-application
using System.Windows.Forms;

public delegate void MouseMovedEvent();

public class GlobalMouseHandler : IMessageFilter
{
    public event MouseMovedEvent TheMouseMoved;

    public bool PreFilterMessage(ref Message m)
    {
        if (m.Msg == WM.MOUSEMOVE)
        {
            if (TheMouseMoved != null)
            {
                TheMouseMoved();
            }
        }
        // always allow message to continue to the next filter control
        return false;
    }
}