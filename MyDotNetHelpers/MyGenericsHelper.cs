using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyMiscHelpers
{
	public static class MyGenericsHelper
	{
		/// <summary>
		/// IDisposable を安全に Dispose する汎用メソッド。
		/// </summary>
		/// <typeparam name="Type">IDisposable</typeparam>
		/// <param name="obj"></param>
		public static void SafeDispose<Type>(ref Type obj)
			where Type : IDisposable
		{
			if (obj != null)
			{
				obj.Dispose();
				obj = default(Type); // null 非許容型への対応。
			}
		}

		/// <summary>
		/// ジェネリクスによる汎用 Clamp メソッド。
		/// </summary>
		/// <typeparam name="Type"></typeparam>
		/// <param name="x"></param>
		/// <param name="min"></param>
		/// <param name="max"></param>
		/// <returns></returns>
		public static Type Clamp<Type>(Type x, Type min, Type max)
			where Type : IComparable
		{
			if (x.CompareTo(min) < 0)
				return min;
			else if (x.CompareTo(max) > 0)
				return max;
			else
				return x;
		}

		/// <summary>
		/// ジェネリクスによる汎用 Swap メソッド。
		/// </summary>
		/// <typeparam name="Type"></typeparam>
		/// <param name="a"></param>
		/// <param name="b"></param>
		public static void Swap<Type>(ref Type a, ref Type b)
		{
			Type c = b;
			b = a;
			a = c;
		}

		public static string GetMemberName<T>(System.Linq.Expressions.Expression<Func<T>> e)
		{
			var memberExp = (System.Linq.Expressions.MemberExpression)e.Body;
			return memberExp.Member.Name;
		}
	}
}
