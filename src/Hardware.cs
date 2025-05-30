﻿/*
 
  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.
 
  Copyright (C) 2009-2010 Paul Werelds <paul@werelds.net>
	
*/
using System.Management.Instrumentation;
using LibreHardwareMonitor.Hardware;

namespace LibreHardwareMonitor.WMI {
  [InstrumentationClass(InstrumentationType.Instance)]
  public class Hardware : IWmiObject {
    #region WMI Exposed
    
    public string HardwareType { get; private set; }
    public string Identifier { get; private set; }
    public string Name { get; private set; }
    public string Parent { get; private set; }

    #endregion

    public Hardware(IHardware hardware) {
      Name = hardware.Name;
      Identifier = hardware.Identifier.ToString();
      HardwareType = hardware.HardwareType.ToString();
      Parent = (hardware.Parent != null)
        ? hardware.Parent.Identifier.ToString()
        : "";
    }

    public void Update() { }
  }
}
