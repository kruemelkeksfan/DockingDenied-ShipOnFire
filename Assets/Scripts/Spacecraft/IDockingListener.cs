using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IDockingListener
{
    void Docked(DockingPort port, DockingPort otherPort);
    void Undocked(DockingPort port, DockingPort otherPort);
}
