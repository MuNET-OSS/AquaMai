using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using LibUsbDotNet;
using LibUsbDotNet.Main;

namespace MonoLibUsb.Profile;

public class MonoUsbProfileList : IEnumerable<MonoUsbProfile>, IEnumerable
{
	private object LockProfileList = new object();

	private List<MonoUsbProfile> mList = new List<MonoUsbProfile>();

	public int Count
	{
		get
		{
			lock (LockProfileList)
			{
				return mList.Count;
			}
		}
	}

	public MonoUsbProfile this[int index]
	{
		get
		{
			lock (LockProfileList)
			{
				return mList[index];
			}
		}
	}

	public event EventHandler<AddRemoveEventArgs> AddRemoveEvent;

	private static bool FindDiscoveredFn(MonoUsbProfile check)
	{
		return check.mDiscovered;
	}

	private static bool FindUnDiscoveredFn(MonoUsbProfile check)
	{
		return !check.mDiscovered;
	}

	private void FireAddRemove(MonoUsbProfile monoUSBProfile, AddRemoveType addRemoveType)
	{
		this.AddRemoveEvent?.Invoke(this, new AddRemoveEventArgs(monoUSBProfile, addRemoveType));
	}

	private void SetDiscovered(bool discovered)
	{
		using IEnumerator<MonoUsbProfile> enumerator = GetEnumerator();
		while (enumerator.MoveNext())
		{
			enumerator.Current.mDiscovered = discovered;
		}
	}

	private void syncWith(MonoUsbProfileList newList)
	{
		SetDiscovered(discovered: false);
		newList.SetDiscovered(discovered: true);
		int count = newList.mList.Count;
		for (int i = 0; i < count; i++)
		{
			MonoUsbProfile monoUsbProfile = newList.mList[i];
			int index;
			if ((index = mList.IndexOf(monoUsbProfile)) == -1)
			{
				monoUsbProfile.mDiscovered = true;
				mList.Add(monoUsbProfile);
				FireAddRemove(monoUsbProfile, AddRemoveType.Added);
			}
			else
			{
				mList[index].mDiscovered = true;
				monoUsbProfile.mDiscovered = false;
			}
		}
		newList.mList.RemoveAll(FindDiscoveredFn);
		newList.Close();
		foreach (MonoUsbProfile item in mList.ToList())
		{
			if (!item.mDiscovered)
			{
				FireAddRemove(item, AddRemoveType.Removed);
				item.Close();
			}
		}
		mList.RemoveAll(FindUnDiscoveredFn);
	}

	public int Refresh(MonoUsbSessionHandle sessionHandle)
	{
		lock (LockProfileList)
		{
			MonoUsbProfileList monoUsbProfileList = new MonoUsbProfileList();
			MonoUsbProfileListHandle monoUSBProfileListHandle;
			int deviceList = MonoUsbApi.GetDeviceList(sessionHandle, out monoUSBProfileListHandle);
			if (deviceList < 0 || monoUSBProfileListHandle.IsInvalid)
			{
				UsbError.Error(ErrorCode.MonoApiError, deviceList, "Refresh:GetDeviceList Failed", this);
				return deviceList;
			}
			int num = deviceList;
			foreach (MonoUsbProfileHandle item in monoUSBProfileListHandle)
			{
				monoUsbProfileList.mList.Add(new MonoUsbProfile(item));
				num--;
				if (num <= 0)
				{
					break;
				}
			}
			syncWith(monoUsbProfileList);
			monoUSBProfileListHandle.Dispose();
			return deviceList;
		}
	}

	public void Close()
	{
		lock (LockProfileList)
		{
			foreach (MonoUsbProfile m in mList)
			{
				m.Close();
			}
			mList.Clear();
		}
	}

	public IEnumerator<MonoUsbProfile> GetEnumerator()
	{
		lock (LockProfileList)
		{
			return mList.GetEnumerator();
		}
	}

	IEnumerator IEnumerable.GetEnumerator()
	{
		return GetEnumerator();
	}

	public List<MonoUsbProfile> GetList()
	{
		lock (LockProfileList)
		{
			return new List<MonoUsbProfile>(mList);
		}
	}
}
