using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IDockingListener
{
    void Docked(DockingPort port);
    void Undocked(DockingPort port);
}
