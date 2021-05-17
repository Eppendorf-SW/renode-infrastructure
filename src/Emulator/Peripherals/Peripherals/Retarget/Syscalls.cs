//
// Copyright (c) 2010-2019 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;
using System.Threading;
using Antmicro.Migrant;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.TAPHelper;
using System.Runtime.InteropServices;

namespace Antmicro.Renode.Peripherals.Retarget
{   
    [AllowedTranslations(AllowedTranslation.WordToDoubleWord | AllowedTranslation.ByteToDoubleWord)]
    public class Syscalls : BasicDoubleWordPeripheral
    {
        public Syscalls(Machine machine) : base(machine)
        {
            DefineRegisters();
        }

        public override void Reset()
        {
            base.Reset();
        }
       
        private void DefineRegisters()
        {
            SyscallsRegister.Call.Define(this, 0x0, "Call")
                .WithValueField(0, 32, valueProviderCallback: _ =>
                    { 
                        return (uint) call;
                    }, writeCallback: (_, value) =>
                    { 
                        errno.Value = 0;
                        call = (Call) value;
                        switch (call)
                        {
                            case Call.Open:
                                CallOpen();
                                break;
                            case Call.Close:
                                CallClose();
                                break;
                            case Call.Write:
                                CallWrite();
                                break;
                            case Call.Read:
                                CallRead();
                                break;
                            case Call.Lseek:
                                CallLseek();
                                break;
                            case Call.Fstat:
                                CallFstat();
                                break;
                            case Call.Isatty:
                                CallIsatty();
                                break;
                            case Call.Chdir:
                                CallChdir();
                                break;
                            default:
                                throw new Exception("Invalid syscall: " + call);
                        }
                    }

                );
            SyscallsRegister.Fd.Define(this, 0x0, "Fd")
                .WithValueField(0, 32, out fd);
            
            SyscallsRegister.BufferSize.Define(this, 0x0, "BufferSize")
                .WithValueField(0, 32, out bufferSize);
            SyscallsRegister.Buffer.Define(this, 0x0, "Buffer")
                .WithValueField(0, 32, out buffer);
            
            SyscallsRegister.Oflag.Define(this, 0x0, "Oflag")
                .WithValueField(0, 32, out oflag);
            SyscallsRegister.Pmode.Define(this, 0x0, "Pmode")
                .WithValueField(0, 32, out pmode);
            
            SyscallsRegister.Status.Define(this, 0x0, "Status")
                .WithValueField(0, 32, out status);
            SyscallsRegister.Errno.Define(this, 0x0, "Errno")
                .WithValueField(0, 32, out errno);
            
            SyscallsRegister.Offset.Define(this, 0x0, "Offset")
                .WithValueField(0, 32, out offset);
            SyscallsRegister.Origin.Define(this, 0x0, "Origin")
                .WithValueField(0, 32, out origin);
            SyscallsRegister.Position.Define(this, 0x0, "Position")
                .WithValueField(0, 32, out position);
            
            SyscallsRegister.St_dev.Define(this, 0x0, "St_dev")
                .WithValueField(0, 32, out st_dev);
            SyscallsRegister.St_ino.Define(this, 0x0, "St_ino")
                .WithValueField(0, 32, out st_ino);
            SyscallsRegister.St_mode.Define(this, 0x0, "St_mode")
                .WithValueField(0, 32, out st_mode);
            SyscallsRegister.St_nlink.Define(this, 0x0, "St_nlink")
                .WithValueField(0, 32, out st_nlink);
            SyscallsRegister.St_uid.Define(this, 0x0, "St_uid")
                .WithValueField(0, 32, out st_uid);
            SyscallsRegister.St_gid.Define(this, 0x0, "St_gid")
                .WithValueField(0, 32, out st_gid);
            SyscallsRegister.St_size.Define(this, 0x0, "St_size")
                .WithValueField(0, 32, out st_size);
            SyscallsRegister.St_rdev.Define(this, 0x0, "St_rdev")
                .WithValueField(0, 32, out st_rdev);
            SyscallsRegister.St_blksize.Define(this, 0x0, "St_blksize")
                .WithValueField(0, 32, out st_blksize);
            SyscallsRegister.St_blocks.Define(this, 0x0, "St_blocks")
                .WithValueField(0, 32, out st_blocks);
            SyscallsRegister.St_atim.Define(this, 0x0, "St_atim")
                .WithValueField(0, 32, out st_atim);
            SyscallsRegister.St_mtim.Define(this, 0x0, "St_mtim")
                .WithValueField(0, 32, out st_mtim);
            SyscallsRegister.St_ctim.Define(this, 0x0, "St_ctim")
                .WithValueField(0, 32, out st_ctim);
            SyscallsRegister.St_atim_ns.Define(this, 0x0, "St_atim_ns")
                .WithValueField(0, 32, out st_atim_ns);
            SyscallsRegister.St_mtim_ns.Define(this, 0x0, "St_mtim_ns")
                .WithValueField(0, 32, out st_mtim_ns);
            SyscallsRegister.St_ctim_ns.Define(this, 0x0, "St_ctim_ns")
                .WithValueField(0, 32, out st_ctim_ns);
        }

        private void CallOpen()
        {
            var data = machine.SystemBus.ReadBytes((ulong) buffer.Value, (int) bufferSize.Value);
            IntPtr dataPtr = Marshal.AllocHGlobal(data.Length);
            Marshal.Copy(data, 0, dataPtr, data.Length);
            
            int current_status = LibC.open(dataPtr, TransformOpenFlags(), (int)pmode.Value);
            fd.Value = (uint) current_status;
            if (current_status == -1)
            {
                SetErrorNumber();
            }
            
            Marshal.FreeHGlobal(dataPtr);
            call = Call.Done;
        }

        private void CallClose() 
        {
            int current_status = LibC.close((int)fd.Value);
            status.Value = (uint) current_status;
            if (current_status == -1)
            {
                SetErrorNumber();
            }
            call = Call.Done;
        }

        private void CallWrite()
        {
            var data = machine.SystemBus.ReadBytes((ulong) buffer.Value, (int) bufferSize.Value);
            IntPtr dataPtr = Marshal.AllocHGlobal(data.Length);
            Marshal.Copy(data, 0, dataPtr, data.Length);
            
            int current_status = LibC.write((int)fd.Value, dataPtr, (int)bufferSize.Value);
            status.Value = (uint) current_status;
            if (current_status == -1)
            {
                SetErrorNumber();
            }
            
            Marshal.FreeHGlobal(dataPtr);
            call = Call.Done;
        }

        private void CallRead()
        {
           IntPtr dataPtr = Marshal.AllocHGlobal((int)bufferSize.Value); 
           int readed = LibC.read((int)fd.Value, dataPtr, (int)bufferSize.Value);
           if (readed == -1)
           {
               SetErrorNumber();
           }
           else
           {
               byte[] data = new byte[readed];
               Marshal.Copy(dataPtr, data, 0, readed);
               machine.SystemBus.WriteBytes(data, (ulong) buffer.Value, readed);
               Marshal.FreeHGlobal(dataPtr);
               
           }
           status.Value = (uint) readed;
           call = Call.Done;
        }

        private void CallLseek()
        {
            int current_status = LibC.lseek((int) fd.Value, Convert.ToInt64((int)offset.Value), (int) origin.Value);
            position.Value = (uint) current_status;
            if (current_status == -1)
            {
                SetErrorNumber();
            }
            call = Call.Done;
        }
        
        unsafe private void CallFstat()
        {
            LibC.stat fstats = new LibC.stat();
        
            int current_status = LibC._fstat(0, (int) fd.Value, &fstats);  
            status.Value = (uint) current_status;
            if (current_status == -1)
            {
                SetErrorNumber();
            }
        
            st_dev.Value = (uint) fstats.st_dev;
            st_ino.Value = (uint) fstats.st_ino;
            st_mode.Value = (uint) fstats.st_mode;
            st_nlink.Value = (uint) fstats.st_nlink;
            st_uid.Value = (uint) fstats.st_uid;
            st_gid.Value = (uint) fstats.st_gid;
            st_size.Value = (uint) fstats.st_size;
            st_rdev.Value = (uint) fstats.st_rdev;
            st_blksize.Value = (uint) fstats.st_blksize;
            st_blocks.Value = (uint) fstats.st_blocks;
            st_atim.Value = (uint) fstats.st_atim.tv_sec;
            st_mtim.Value = (uint) fstats.st_mtim.tv_sec;
            st_ctim.Value = (uint) fstats.st_ctim.tv_sec;
            st_atim_ns.Value = (uint) fstats.st_atim.tv_nsec;
            st_mtim_ns.Value = (uint) fstats.st_mtim.tv_nsec;
            st_ctim_ns.Value = (uint) fstats.st_ctim.tv_nsec;
        
            call = Call.Done;
        }

        private void CallIsatty()
        {
            int current_status = LibC.isatty((char) fd.Value);
            status.Value = (uint) current_status;
            if (current_status == 0)
            {
                SetErrorNumber();
            }
            call = Call.Done;
        }

        private void CallChdir()
        {
            var data = machine.SystemBus.ReadBytes((ulong) buffer.Value, (int) bufferSize.Value);
            IntPtr dataPtr = Marshal.AllocHGlobal(data.Length);
            Marshal.Copy(data, 0, dataPtr, data.Length);

            int current_status = LibC.chdir(dataPtr);
            status.Value = (uint) current_status;
            if (current_status == -1)
            {
                SetErrorNumber();
            }

            Marshal.FreeHGlobal(dataPtr);
            call = Call.Done;
        }

        private void SetErrorNumber()
        {
            errno.Value = (uint)Marshal.GetLastWin32Error();
        }

        private int TransformOpenFlags()
        {
            int usedFlags = (int)oflag.Value;
            int convertedFlags = 0;
            foreach (var flag in oflagConvert)
            {
                if ((usedFlags & flag.Item2) != 0)
                {
                    convertedFlags = convertedFlags | flag.Item1;
                }
            }

            return convertedFlags;
        }

        private Tuple<int, int>[] oflagConvert =
        {
            // (Linux, ARM)
            Tuple.Create(0, 0), // O_RDONLY
            Tuple.Create(1, 1), // O_WRONLY
            Tuple.Create(2, 2), // O_RDWR
            Tuple.Create(1024, 8), //O_APPEND
            Tuple.Create(64, 512), // O_CREAT
            Tuple.Create(512, 1024), // O_TRUNC
            Tuple.Create(128, 2048), // O_EXCL
            Tuple.Create(1052672, 8192), // O_SYNC
            Tuple.Create(4096, 8192), // O_DSYNC
            Tuple.Create(1052672, 8192), // O_RSYNC
            Tuple.Create(2048, 16384), // O_NDELAY
            Tuple.Create(2048, 16384), // O_NONBLOCK
            Tuple.Create(256, 32768), // O_NOCTTY
            Tuple.Create(524288, 262144), // O_CLOEXEC
            Tuple.Create(4259840, 8388608), // O_TMPFILE
            Tuple.Create(262144, 16777216), // O_NOATIME
            Tuple.Create(2097152, 33554432), // O_PATH
            Tuple.Create(131072, 131072), // O_NOFOLLOW
            Tuple.Create(64, 65536) // O_CREAT
        };
        
        private Call call = Call.Done;
        private IValueRegisterField fd;
        private IValueRegisterField bufferSize;
        private IValueRegisterField buffer;
        
        private IValueRegisterField oflag;
        private IValueRegisterField pmode;
        
        private IValueRegisterField offset;
        private IValueRegisterField origin;
        private IValueRegisterField position;
        
        private IValueRegisterField status;
        private IValueRegisterField errno;
        
        private IValueRegisterField st_dev;
        private IValueRegisterField st_ino;
        private IValueRegisterField st_mode;
        private IValueRegisterField st_nlink;
        private IValueRegisterField st_uid;
        private IValueRegisterField st_gid;
        private IValueRegisterField st_size;
        private IValueRegisterField st_rdev;
        private IValueRegisterField st_blksize;
        private IValueRegisterField st_blocks;
        private IValueRegisterField st_atim;
        private IValueRegisterField st_mtim;
        private IValueRegisterField st_ctim;
        private IValueRegisterField st_atim_ns;
        private IValueRegisterField st_mtim_ns;
        private IValueRegisterField st_ctim_ns;


        private enum Call : uint
        {
            Done = 0,
            Open = 1,
            Close = 2,
            Write = 3,
            Read = 4,
            Lseek = 5,
            Fstat = 6, 
            Isatty = 7,
            Chdir = 8
        }

        private enum SyscallsRegister : long
        {
            Call = 0x0,
            Fd = 0x4,
            
            BufferSize = 0x8,
            Buffer = 0xC,
            
            Oflag = 0x10,
            Pmode = 0x14,
            
            Status = 0x18,
            Errno = 0x1C,
            
            Offset = 0x20,
            Origin = 0x24,
            Position = 0x28,
            
            St_dev = 0x2C,
            St_ino = 0x30,
            St_mode = 0x34,
            St_nlink = 0x38,
            St_uid = 0x3C,
            St_gid = 0x40,
            St_size = 0x44,
            St_rdev = 0x48,
            St_blksize = 0x4C,
            St_blocks = 0x50,
            St_atim = 0x54,
            St_mtim = 0x58,
            St_ctim = 0x5C,
            St_atim_ns = 0x60,
            St_mtim_ns = 0x64,
            St_ctim_ns = 0x68
        }
    }
}
