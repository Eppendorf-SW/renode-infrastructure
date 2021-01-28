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
using System.Linq;
using System.Threading;
using Antmicro.Migrant;
using Antmicro.Renode.Core.Structure.Registers;

namespace Antmicro.Renode.Peripherals.UART
{
    [AllowedTranslations(AllowedTranslation.WordToDoubleWord | AllowedTranslation.ByteToDoubleWord)]
    public class RTT : BasicDoubleWordPeripheral, IUART
    {
        public RTT(Machine machine) : base(machine)
        {
            DefineRegisters();
        }
        
        public void WriteChar(byte value)
        {
            lock (receiveFifo)
            {
                receiveFifo.Add(value);
                if (receiveFifo.Count == dataSize.Value)
                {
                   Monitor.Pulse(receiveFifo); 
                }
            }
        }

        public override void Reset()
        {
            base.Reset();
            lock (receiveFifo)
            {
                receiveFifo.Clear();
            }
        }

        [field: Transient]
        public event Action<byte> CharReceived;

        private void DefineRegisters()
        {
            Register.Transfer.Define(this, 0x0, "Transfer")
                .WithValueField(0, 32, valueProviderCallback: _ =>
                {
                    TransferIn();
                    return (uint)dataTransfer;
                }, writeCallback: (_, value) =>
                {
                    dataTransfer = (Transfer)value;
                    TransferOut();
                    TransferIn();
                });
            Register.Size.Define(this, 0x0, "Size")
                .WithValueField(0, 32, out dataSize);
            Register.Data.Define(this, 0x0, "Data")
                .WithValueField(0, 32, out dataPtr);
        }
        
        private void TransferOut()
        {
            if (dataTransfer == Transfer.Out)
            {
                foreach (var b in machine.SystemBus.ReadBytes((ulong) dataPtr.Value, (int) dataSize.Value))
                {
                    CharReceived?.Invoke(b);
                }

                dataTransfer = Transfer.Done;
            }
        }

        private void TransferIn()
        {
            if (dataTransfer == Transfer.In)
            {
                lock (receiveFifo)
                {
                    if (receiveFifo.Count < dataSize.Value)
                    {
                        Monitor.Wait(receiveFifo);
                    }

                    machine.SystemBus.WriteBytes(receiveFifo.ToArray(), (ulong) (dataPtr.Value), 0, dataSize.Value);
                    receiveFifo.RemoveRange(0, (int)dataSize.Value);
                    dataTransfer = Transfer.Done;
                }
            }
        }
        
        private IValueRegisterField dataPtr;
        private Transfer dataTransfer = Transfer.Done;
        private IValueRegisterField dataSize;
        private readonly Queue<byte> receiveFifo2 = new Queue<byte>();
        private readonly List<byte> receiveFifo = new List<byte>();

        public uint BaudRate
        {
            get { return 1920; }
        }

        public Bits StopBits {
            get
            {
                return Bits.One;
            }
        }

        public Parity ParityBit
        {
            get {
                return Parity.Even;
            }
        }

        private enum Transfer : uint
        {
            Done = 0,
            Out = 1,
            In = 2
        }
        
        private enum Register : long
        {
            Transfer = 0x0,
            Size = 0x4,
            Data = 0x8
        }
    }
}
