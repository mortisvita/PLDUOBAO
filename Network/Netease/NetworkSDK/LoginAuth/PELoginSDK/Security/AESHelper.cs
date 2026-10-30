using System;
using System.IO;
using System.Security.Cryptography;

namespace Noya.LocalServer.Common.Cryptography;

public class AESHelper
{
	public static byte[] AES_CBC_Decrypt(byte[] key, byte[] data, byte[] iv)
	{
		using Aes aes = Aes.Create();
		aes.KeySize = key.Length * 8;
		aes.BlockSize = 128;
		aes.Key = key;
		aes.IV = iv;
		aes.Mode = CipherMode.CBC;
		aes.Padding = PaddingMode.None;
		using ICryptoTransform cryptoTransform = aes.CreateDecryptor();
		byte[] array = new byte[data.Length];
		cryptoTransform.TransformBlock(data, 0, data.Length, array, 0);
		return array;
	}

	public static byte[] AES_CBC_Encrypt(byte[] key, byte[] data, byte[] iv)
	{
		using Aes aes = Aes.Create();
		aes.KeySize = key.Length * 8;
		aes.BlockSize = 128;
		aes.Key = key;
		aes.IV = iv;
		aes.Mode = CipherMode.CBC;
		aes.Padding = PaddingMode.None;
		using ICryptoTransform cryptoTransform = aes.CreateEncryptor();
		byte[] array = new byte[data.Length];
		cryptoTransform.TransformBlock(data, 0, data.Length, array, 0);
		return array;
	}

	public static byte[] AES_CBC256_Encrypt(byte[] key, byte[] toEncrypt, byte[] iv)
	{
		int num = 16 - toEncrypt.Length % 16;
		if (num == 16)
		{
			num = 0;
		}
		int num2 = 0;
		if (toEncrypt.Length < 16)
		{
			num2 = 1;
		}
		else
		{
			num2 = toEncrypt.Length / 16;
			if (num != 0)
			{
				num2++;
			}
		}
		byte[] array = new byte[num2 * 16];
		Array.Copy(toEncrypt, array, toEncrypt.Length);
		for (int i = 0; i < num; i++)
		{
			array[i + toEncrypt.Length] = (byte)num;
		}
		using var aes = Aes.Create();
		aes.Key = key;
		aes.IV = iv;
		aes.Mode = CipherMode.CBC;
		aes.Padding = PaddingMode.None;
		return aes.CreateEncryptor().TransformFinalBlock(array, 0, array.Length);
	}

	public static ICryptoTransform getCipherInstance(byte[] Key, bool encrypt = true)
	{
		if (Key.Length < 16)
		{
			Array.Resize(ref Key, 16);
		}
		else if (Key.Length < 24)
		{
			Array.Resize(ref Key, 24);
		}
		else if (Key.Length < 32)
		{
			Array.Resize(ref Key, 32);
		}
		else
		{
			Array.Resize(ref Key, 32);
		}
		Aes aes = Aes.Create();
		aes.Mode = CipherMode.ECB;
		aes.KeySize = 128;
		aes.Key = Key;
		aes.Padding = PaddingMode.PKCS7;
		aes.BlockSize = 128;
		if (!encrypt)
		{
			return aes.CreateDecryptor();
		}
		return aes.CreateEncryptor();
	}

	public static byte[] encrypt(byte[] key, byte[] source)
	{
		return getCipherInstance(key).TransformFinalBlock(source, 0, source.Length);
	}

	public static byte[] decrypt(byte[] key, byte[] source)
	{
		return getCipherInstance(key, encrypt: false).TransformFinalBlock(source, 0, source.Length);
	}

	public static byte[] AES_CFB_Decrypt(byte[] key, byte[] data, byte[] iv)
	{
		try
		{
			MemoryStream memoryStream = new MemoryStream(data);
			using Aes aes = Aes.Create();
			aes.Key = key;
			aes.IV = iv;
			aes.Mode = CipherMode.CFB;
			aes.Padding = PaddingMode.Zeros;
			CryptoStream cryptoStream = new CryptoStream(memoryStream, aes.CreateDecryptor(), CryptoStreamMode.Read);
			try
			{
				byte[] array = new byte[data.Length + 32];
				int num = cryptoStream.Read(array, 0, data.Length + 32);
				byte[] array2 = new byte[num];
				Array.Copy(array, 0, array2, 0, num);
				return array2;
			}
			finally
			{
				cryptoStream.Close();
				memoryStream.Close();
				aes.Clear();
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine(ex.Message);
			return null;
		}
	}

	public static byte[] AES_ECB_Encrypt(byte[] key, byte[] data)
	{
		using Aes aes = Aes.Create();
		aes.KeySize = key.Length * 8;
		aes.BlockSize = 128;
		aes.Key = key;
		aes.Mode = CipherMode.ECB;
		aes.Padding = PaddingMode.None;
		using ICryptoTransform cryptoTransform = aes.CreateEncryptor();
		byte[] array = new byte[data.Length];
		cryptoTransform.TransformBlock(data, 0, data.Length, array, 0);
		return array;
	}
    public static byte[] AES_ECB_Decrypt(byte[] key, byte[] data)
    {
        using Aes aes = Aes.Create();
        aes.KeySize = key.Length * 8;
        aes.BlockSize = 128;
        aes.Key = key;
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.None;

        using ICryptoTransform cryptoTransform = aes.CreateDecryptor();
        byte[] decrypted = new byte[data.Length];
        cryptoTransform.TransformBlock(data, 0, data.Length, decrypted, 0);
        return decrypted;
    }

}
