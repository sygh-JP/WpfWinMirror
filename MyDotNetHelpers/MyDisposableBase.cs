using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyMiscHelpers
{
	/// <summary>
	/// IDisposable の実装を補助する抽象クラス。
	/// </summary>
	public abstract class MyDisposableBase : IDisposable
	{
		private bool isDisposed = false;
		private readonly object lockObj = new object();

		~MyDisposableBase()
		{
			// リソースの解放。
			// マネージ オブジェクトは GC で寿命管理されているため、
			// C# のデストラクタはタイミング不定どころか終了時にすら呼ばれない可能性があることに注意。
			// C# においてデストラクタを記述する意味は、
			// 「Dispose() がもし呼ばれなかった場合、最終防衛ラインとして GC 回収されるタイミングでリソースを破棄します」という保証にしかならない。
			this.OnDispose(false);
		}

		protected abstract void OnDisposeManagedResources();

		protected abstract void OnDisposeUnmamagedResources();

		private void OnDispose(bool disposesManagedResources)
		{
			// よくある IDisposable 実装のサンプルでは、
			// protected virtual void Dispose(bool dispose) もしくは
			// protected virtual void Dispose(bool disposing) と宣言されている。
			// https://msdn.microsoft.com/en-us/library/fs2xkftw.aspx
			// protected virtual な仮想メソッドを定義しているのは、
			// 派生クラスでもリソースを追加管理するようなときに備えるためらしい。
			// この場合、派生クラスでオーバーライドする際には、base 経由で基底クラスの Dispose(bool) をきちんと呼び出すようにすればよいが、ややこしい。
			// 本クラスでは、派生クラスでオーバーライドする箇所（カスタマイズポイント）をさらに限定する。
			// 「C++ Coding Standards」項目39「仮想関数を非 public に、public 関数を非仮想にすることを検討しよう」や、
			// 「Effective C++ 第3版」第6章35項「仮想関数の代わりになるものを考えよう」を参照のこと。

			lock (this.lockObj)
			{
				if (this.isDisposed)
				{
					// 既に呼び出し済みであるならば何もしない。
					return;
				}

				if (disposesManagedResources)
				{
					// TODO: IDisposable 実装クラスなどのマネージ リソースの解放はココで行なう。
					this.OnDisposeManagedResources();
				}

				// TODO: IntPtr ハンドルなどのアンマネージ リソースの解放はココで行なう。
				this.OnDisposeUnmamagedResources();

				this.isDisposed = true;
			}
		}

		protected void ThrowExceptionIfDisposed()
		{
			if (this.isDisposed)
			{
				throw new ObjectDisposedException(this.GetType().ToString());
			}
		}

		/// <summary>
		/// IDisposable.Dispose() の実装。
		/// </summary>
		public void Dispose()
		{
			// リソースの解放。
			this.OnDispose(true);

			// このオブジェクトのデストラクタを GC 対象外とする。
			GC.SuppressFinalize(this);
		}
	}
}
