namespace hjo.btgyro
{
	public class ReceiverStateMachine : Receiver
	{
		private readonly System.Action<int> changeX;
		private readonly System.Action<int> changeY;
		private readonly System.Action<int> changeZ;

		private readonly char[] valueBuffer = new char[4];
		private int valueBufferPosition;

		private bool stateX;
		private bool stateY;
		private bool stateZ;

		public ReceiverStateMachine(
			System.Action<int> changeX,
			System.Action<int> changeY,
			System.Action<int> changeZ)
		{
			this.changeX = changeX;
			this.changeY = changeY;
			this.changeZ = changeZ;
		}

		private void Reset()
		{
			this.valueBufferPosition = 0;
			this.valueBuffer[0] = '0';
			this.valueBuffer[1] = '0';
			this.valueBuffer[2] = '0';
			this.valueBuffer[3] = '0';
			this.stateX = false;
			this.stateY = false;
			this.stateZ = false;
		}

		public void Receive(char next)
		{
			if (next == 'X')
			{
				this.Flush();
				this.Reset();
				this.stateX = true;
				return;
			}
			else if (next == 'Y')
			{
				this.Flush();
				this.Reset();
				this.stateY = true;
				return;
			}
			else if (next == 'Z')
			{
				this.Flush();
				this.Reset();
				this.stateZ = true;
				return;
			}

			if (!IsDigit(next))
			{
				this.Reset();
			};

			this.valueBuffer[this.valueBufferPosition] = next;

			if (this.valueBufferPosition == 3)
			{
				this.Flush();
				this.Reset();
			}
			else
			{
				this.valueBufferPosition++;
			}
		}

		private static bool IsDigit(char next)
		{
			return
				next == '-' || next == '0' ||
				next == '1' || next == '2' || next == '3' ||
				next == '4' || next == '5' || next == '6' ||
				next == '7' || next == '8' || next == '9';
		}

		private void Flush()
		{
			if (this.valueBufferPosition == 0) return;

			char[] v = new char[this.valueBufferPosition + 1];
			for (int i=0; i<this.valueBufferPosition; i++)
			{
				v[i] = this.valueBuffer[i];
			}

			int value = int.Parse(v);

			if (this.stateX)
			{
				this.changeX(value);
			}
			else if (this.stateY)
			{
				this.changeY(value);
			}
			else if (this.stateZ)
			{
				this.changeZ(value);
			}
		}
	}
}