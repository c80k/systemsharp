/**
 * Copyright 2011-2012 Christian Köllner
 * 
 * This file is part of System#.
 *
 * System# is free software: you can redistribute it and/or modify it under 
 * the terms of the GNU Lesser General Public License (LGPL) as published 
 * by the Free Software Foundation, either version 3 of the License, or (at 
 * your option) any later version.
 *
 * System# is distributed in the hope that it will be useful, but WITHOUT ANY
 * WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS 
 * FOR A PARTICULAR PURPOSE. See the GNU General Public License for more 
 * details.
 *
 * You should have received a copy of the GNU General Public License along 
 * with System#. If not, see http://www.gnu.org/licenses/lgpl.html.
 * 
 * */

#if false

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml.Serialization;
using SystemSharp.Components;
using SystemSharp.Meta;

namespace SystemSharp.Interop.Xilinx.PerfCache
{
    public class PerformanceCache
    {
        private static string _cacheRootPath;
        private static XmlSerializer _deviceDataSerializer;
        private static XmlSerializer _ipCoreDataSerializer;
        private static DeviceRecords _deviceRecords;

        private static string CacheRootPath
        {
            get
            {
                if (_cacheRootPath == null)
                {
                    string appdata = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                    _cacheRootPath = Path.Combine(appdata, @"SystemSharp\XilinxInterop\PerfCache");
                }
                return _cacheRootPath;
            }
        }

        private static string DeviceCachePath
        {
            get { return Path.Combine(CacheRootPath, "devices.xml"); }
        }

        private static XmlSerializer DeviceDataSerializer
        {
            get
            {
                if (_deviceDataSerializer == null)
                    _deviceDataSerializer = new XmlSerializer(typeof(DeviceRecords));
                return _deviceDataSerializer;
            }
        }

        private static XmlSerializer IPCoreDataSerializer
        {
            get
            {
                if (_ipCoreDataSerializer == null)
                    _ipCoreDataSerializer = new XmlSerializer(typeof(IPCore));
                return _ipCoreDataSerializer;
            }
        }

        private static DeviceRecords LoadDeviceRecords()
        {
            var ser = DeviceDataSerializer;
            try
            {
                using (var rd = new StreamReader(DeviceCachePath))
                {
                    return (DeviceRecords)ser.Deserialize(rd);
                }
            }
            catch (FileNotFoundException)
            {
                var recs = new DeviceRecords();
                recs.device = new DeviceRecordsDevice[0];
                return recs;
            }
        }

        private static DeviceRecords DeviceRecords
        {
            get
            {
                if (_deviceRecords == null)
                    _deviceRecords = LoadDeviceRecords();
                return _deviceRecords;
            }
        }

        public static ResourceRecord QueryDeviceResources(EDevice device)
        {
            var recs = DeviceRecords;
            string devName = device.ToString();
            var hit = recs.device
                .Where(d => d.name == devName)
                .FirstOrDefault();
            if (hit == null)
                return null;
            var result = new ResourceRecord()
            {
                Device = device
            };
            foreach (var resrec in hit.providedResources)
            {
                EDeviceResource res;
                if (DeviceResources.ResolveResourceType(resrec.name, out res))
                {
                    result.AssignResource(res, resrec.amount);
                }
            }
            return result;
        }

        public static void UpdateCache(ResourceRecord rec)
        {
            var dev = new DeviceRecordsDevice();
            dev.name = rec.Device.ToString();
            dev.timestamp = DateTime.Now;
            var rlist = new List<DeviceRecordsDeviceResource>();
            rlist.Add(new DeviceRecordsDeviceResource()
            {
                name = PropEnum.ToString(EDeviceResource.SliceRegisters, EPropAssoc.PARReport),
                amount = rec.SliceRegisters
            });
            rlist.Add(new DeviceRecordsDeviceResource()
            {
                name = PropEnum.ToString(EDeviceResource.SliceLUTs, EPropAssoc.PARReport),
                amount = rec.SliceLUTs
            });
            rlist.Add(new DeviceRecordsDeviceResource()
            {
                name = PropEnum.ToString(EDeviceResource.OccupiedSlices, EPropAssoc.PARReport),
                amount = rec.OccupiedSlices
            });
            rlist.Add(new DeviceRecordsDeviceResource()
            {
                name = PropEnum.ToString(EDeviceResource.RAMB18s, EPropAssoc.PARReport),
                amount = rec.RAMB18s
            });
            rlist.Add(new DeviceRecordsDeviceResource()
            {
                name = PropEnum.ToString(EDeviceResource.RAMB36s, EPropAssoc.PARReport),
                amount = rec.RAMB36s
            });
            rlist.Add(new DeviceRecordsDeviceResource()
            {
                name = PropEnum.ToString(EDeviceResource.DSP48E1s, EPropAssoc.PARReport),
                amount = rec.DSP48E1s
            });
            dev.providedResources = rlist.ToArray();
            var drecs = DeviceRecords;
            int idx = Array.FindIndex(drecs.device, d => d.name == dev.name);
            if (idx < 0)
                drecs.device = drecs.device.Concat(new DeviceRecordsDevice[] { dev }).ToArray();
            else
                drecs.device[idx] = dev;
            using (var wr = new StreamWriter(DeviceCachePath))
            {
                DeviceDataSerializer.Serialize(wr, drecs);
                wr.Close();
            }
        }

        private static string GetIPCoreDataPath(string className)
        {
            return Path.Combine(CacheRootPath, "core_" + className + ".xml");
        }

        public static IPCore LoadIPCoreData(string className)
        {
            string path = GetIPCoreDataPath(className);
            try
            {
                using (var rd = new StreamReader(path))
                {
                    var core = (IPCore)_ipCoreDataSerializer.Deserialize(rd);
                    return core;
                }
            }
            catch (FileNotFoundException)
            {
                return null;
            }
        }

        private static string GetClassName(Component core)
        {
            return core.GetType().FullName;
        }

        private static IEnumerable<IPCoreVariantParam> ExtractPerformanceParameters(Component core)
        {
            var ctype = core.GetType();
            var props = ctype
                .GetProperties()
                .Where(p => Attribute.IsDefined(p, typeof(PerformanceRelevant)))
                .Select(p => new IPCoreVariantParam() { name = p.Name, value = p.GetValue(core, new object[0]).ToString() });
            var fields = ctype
                .GetFields()
                .Where(f => Attribute.IsDefined(f, typeof(PerformanceRelevant)))
                .Select(f => new IPCoreVariantParam() { name = f.Name, value = f.GetValue(core).ToString() });
            return props.Concat(fields);
        }

        private static IPCoreVariant LookupVariant(IPCore coreData, string deviceName, string speedGrade, string iseVersion,
            IEnumerable<IPCoreVariantParam> paramSet)
        {
            var coreVar = coreData.variants.Where(
                v => v.deviceName == deviceName &&
                    v.iseVersion == iseVersion &&
                    v.speedGrade == speedGrade &&
                    v.parameters.Join(paramSet, x => x.name, y => y.name, (x, y) => (x.value == y.value))
                                .All(r => r))
                .FirstOrDefault();
            return coreVar;
        }

        public static PerformanceRecord QueryIPCorePerformance(Component core, EDevice device, ESpeedGrade speedGrade, EISEVersion iseVersion)
        {
            string className = GetClassName(core);
            var coreData = LoadIPCoreData(className);
            if (coreData == null)
                return null;
            var paramSet = ExtractPerformanceParameters(core);
            var coreVar = LookupVariant(coreData,
                device.ToString(),
                speedGrade.ToString(),
                iseVersion.ToString(), 
                paramSet);
            if (coreVar == null)
                return null;
            var result = new PerformanceRecord()
            {
                Device = device,
                SpeedGrade = speedGrade,
                ISEVersion = iseVersion,
                MinPeriod = new Time(coreVar.minPeriod, ETimeUnit.ns)
            };
            foreach (var resrec in coreVar.consumedResources)
            {
                EDeviceResource res;
                if (DeviceResources.ResolveResourceType(resrec.name, out res))
                {
                    result.AssignResource(res, resrec.amount);
                }
            }
            return result;
        }

        public static void UpdateCache(Component core, PerformanceRecord prec)
        {
            string className = GetClassName(core);
            var coreData = LoadIPCoreData(className);
            if (coreData == null)
            {
                coreData = new IPCore()
                {
                    className = className,
                    generator = "",
                    variants = new IPCoreVariant[0]
                };
            }
            var paramSet = ExtractPerformanceParameters(core);
            var coreVar = LookupVariant(coreData,
                prec.Device.ToString(),
                prec.SpeedGrade.ToString(),
                prec.ISEVersion.ToString(),
                paramSet);
            var rlist = new List<IPCoreVariantResource>();
            rlist.Add(new IPCoreVariantResource()
            {
                name = PropEnum.ToString(EDeviceResource.SliceRegisters, EPropAssoc.PARReport),
                amount = prec.SliceRegisters
            });
            rlist.Add(new IPCoreVariantResource()
            {
                name = PropEnum.ToString(EDeviceResource.SliceLUTs, EPropAssoc.PARReport),
                amount = prec.SliceLUTs
            });
            rlist.Add(new IPCoreVariantResource()
            {
                name = PropEnum.ToString(EDeviceResource.OccupiedSlices, EPropAssoc.PARReport),
                amount = prec.OccupiedSlices
            });
            rlist.Add(new IPCoreVariantResource()
            {
                name = PropEnum.ToString(EDeviceResource.RAMB18s, EPropAssoc.PARReport),
                amount = prec.RAMB18s
            });
            rlist.Add(new IPCoreVariantResource()
            {
                name = PropEnum.ToString(EDeviceResource.RAMB36s, EPropAssoc.PARReport),
                amount = prec.RAMB36s
            });
            rlist.Add(new IPCoreVariantResource()
            {
                name = PropEnum.ToString(EDeviceResource.DSP48E1s, EPropAssoc.PARReport),
                amount = prec.DSP48E1s
            });
            if (coreVar == null)
            {
                coreVar = new IPCoreVariant()
                {
                    deviceName = prec.Device.ToString(),
                    iseVersion = prec.ISEVersion.ToString(),
                    package = "",
                    speedGrade = prec.SpeedGrade.ToString(),
                    timestamp = DateTime.Now,
                    minPeriod = prec.MinPeriod.ScaleTo(ETimeUnit.ns),
                    parameters = paramSet.ToArray()
                };
                coreData.variants = coreData.variants.Concat(new IPCoreVariant[] { coreVar }).ToArray();
            }
            coreVar.consumedResources = rlist.ToArray();
            string path = GetIPCoreDataPath(className);
            using (var wr = new StreamWriter(path))
            {
                IPCoreDataSerializer.Serialize(wr, coreData);
                wr.Close();
            }
        }
    }
}

#endif