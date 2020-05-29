namespace hngkng.btgyro
{
	using System.Linq;
	using System.Runtime.InteropServices.WindowsRuntime;
	using Windows.Devices.Bluetooth;
	using Windows.Devices.Bluetooth.GenericAttributeProfile;
	using Windows.Devices.Enumeration;
	using Windows.Foundation;
	using Windows.Storage.Streams;

	public class Device
	{
		private readonly System.Action<string> log;
		private readonly System.Action<string> logEol;
		private readonly ReceiverStateMachine receiver;
		private readonly System.Net.Sockets.UdpClient udp;
		private GattCharacteristic txRxChannel;
		private byte[] btData;
		private IBuffer btBuffer;
		private double lastX, lastY, lastZ;
		private byte[] data = new byte[48];

		public Device(
			System.Action<string> log,
			System.Action<string> logWithEol)
		{
			this.log = log;
			this.logEol = logWithEol;

			this.btData = new byte[1];
			this.btData[0] = 83;
			this.btBuffer = this.btData.AsBuffer();

			for (int i=0; i<(8*3); i++)
			{
				this.data[i] = 0;
			}

			this.udp = new System.Net.Sockets.UdpClient();
			this.udp.Connect("localhost", 4242);
			this.receiver = new ReceiverStateMachine(this.SetX, this.SetY, this.SetZ);

			if (!this.BluetoothSetup())
			{
				throw new System.Exception("Could not connect to bluetooth device. Make sure it is paired and connected.");
			}
		}

		public void Calibrate()
		{
			if (this.txRxChannel != default(GattCharacteristic))
			{
				var result = this.txRxChannel.WriteValueAsync(this.btBuffer);
				while (result.Status == AsyncStatus.Started) {}
			}
		}

		public void Teardown()
		{
			if (this.txRxChannel != default(GattCharacteristic))
			{
				this.txRxChannel.ValueChanged -= this.TxRx_ValueChanged;
			}
			this.udp.Close();
			this.udp.Dispose();
		}

		private void SetX(double value)
		{
			this.lastX = value;
			this.Dispatch();
		}

		private void SetY(double value)
		{
			this.lastY = value;
			this.Dispatch();
		}

		private void SetZ(double value)
		{
			this.lastZ = value;
			this.Dispatch();
		}

		private void Dispatch()
		{
			byte[] x = System.BitConverter.GetBytes(this.lastX);
			for (int i=0; i<8; i++)
			{
				this.data[24+i] = x[i];
			}

			byte[] y = System.BitConverter.GetBytes(this.lastY);
			for (int i=0; i<8; i++)
			{
				this.data[32+i] = y[i];
			}

			byte[] z = System.BitConverter.GetBytes(this.lastZ);
			for (int i=0; i<8; i++)
			{
				this.data[40+i] = z[i];
			}

			this.udp.Send(this.data, 48);
		}

		private bool BluetoothSetup()
		{
			BluetoothLEDevice device = this.Waitfor(
				"connection to Bluetooth device",
				BluetoothLEDevice.FromBluetoothAddressAsync(System.Convert.ToUInt64("001580912553", 16)));

			if (device == null)
			{
				this.logEol("Device wasn't found");
				return false;
			}

			GattDeviceServicesResult deviceServices = this.Waitfor(
				"device services", device.GetGattServicesAsync());

			DeviceAccessStatus deviceAccessStatus = this.Waitfor(
				"device access", device.RequestAccessAsync());

			this.logEol($"Device access status: {deviceAccessStatus}");

			System.Guid gyroServiceGuid = System.Guid.Parse("0000ffe0-0000-1000-8000-00805f9b34fb");
			GattDeviceService gyroService = deviceServices.Services.Single(x => x.Uuid.Equals(gyroServiceGuid));

			var gyroServiceAccessStatus = this.Waitfor(
				"gro data service access", gyroService.RequestAccessAsync());

			this.logEol($"Gyro service access status: {gyroServiceAccessStatus}");

			GattCharacteristicsResult characteristics = this.Waitfor(
				"gyro data service", gyroService.GetCharacteristicsAsync());

			this.txRxChannel = characteristics
				.Characteristics
				.SingleOrDefault(x => x.UserDescription.Replace(" ", "") == "TX&RX");

			if (this.txRxChannel == default(GattCharacteristic))
			{
				this.logEol("Couldn't find TXRX channel...disconnected?");
				return false;
			}

			this.txRxChannel.ValueChanged += this.TxRx_ValueChanged;
			return true;
		}

		private void TxRx_ValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
		{
			IBuffer buffer = args.CharacteristicValue;
			using (var dataReader = DataReader.FromBuffer(buffer))
			{
				string received = dataReader.ReadString(buffer.Length);
				foreach (char x in received)
				{
					this.receiver.Receive(x);
				}
			}
		}

		private T Waitfor<T>(string description, IAsyncOperation<T> asyncOperation)
		{
			this.log($"Waiting for {description}");
			while (asyncOperation.Status == AsyncStatus.Started) { }

			if (asyncOperation.Status == AsyncStatus.Completed)
			{
				this.logEol("...done");
				return asyncOperation.GetResults();
			}

			if (asyncOperation.Status == AsyncStatus.Error)
			{
				throw new System.Exception($"Error while waiting for {description}. Code: {asyncOperation.ErrorCode}");
			}
			if (asyncOperation.Status == AsyncStatus.Canceled)
			{
				throw new System.Exception($"Error while waiting for {description}. Cancellation was received.");
			}

			throw new System.Exception($"Unknown status code '{asyncOperation.Status}' while waiting for {description}");
		}

		[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
		private struct Data
		{
			public float Yaw;
			public float Pitch;
			public float Roll;

			public float X;
			public float Y;
			public float Z;
		}

		[System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
		[return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
		private static extern bool SetDllDirectory(string lpPathName);

		[System.Runtime.InteropServices.DllImport("freepie_io.dll")]
		private static extern int freepie_io_6dof_slots();

		[System.Runtime.InteropServices.DllImport("freepie_io.dll", CallingConvention = System.Runtime.InteropServices.CallingConvention.Cdecl)]
		private static extern int freepie_io_6dof_write(int index, int length, Data[] data);

		private const int WriteToIndex = 0;
	}
}