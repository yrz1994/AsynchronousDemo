using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace ConsoleDemo
{
    /// <summary>
    /// 参考：https://github.com/davidfowl/AspNetCoreDiagnosticScenarios/blob/master/AsyncGuidance.md
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
        }

        #region>通过ILSpy观察生成的状态机
        public async Task<string> GetString()
        {
            return "Hello world";
        }

        public async Task<string> DoSomething()
        {
            return await Task.FromResult("Hello world");
        }

        public async Task<string> DoSomething2()
        {
            var a = await Task.FromResult("Hello");
            var b = await Task.FromResult("world");
            return await Task.FromResult($"{a}{b}");
        }
        #endregion

        #region>Asynchrony is viral
        /*
         * TIPS 1: Asynchrony is viral
         * 一旦使用async，所有的调用者都应该是异步的，除非整个调用栈都是异步的，否则异步的努力将无用
         * 许多情况下，部分异步要比全部同步的情况更糟糕。因此最好一次性全部使用异步
         */

        //Bad
        public static int DoSomethingAsync1()
        {
            var result = CallDependencyAsync().Result;
            return result + 1;
        }

        //Good
        public static async Task<int> DoSomethingAsync2()
        {
            var result = await CallDependencyAsync();
            return result + 1;
        }
        #endregion

        #region>Prefer Task.FromResult over Task.Run for pre-computed or trivially computed data
        /*
         * TIPS 3: Prefer Task.FromResult over Task.Run for pre-computed or trivially computed data
         * 对于预先计算好的结果(或简单零碎的计算结果)，不需要调用Task.Run，这将最终将工作项目丢入线程池队列，并且在执行时会立即返回已经计算好的结果，
         * 而使用Task.FromResult，它会创建一个包装好的结果的Task，不会走线程池。
         */

        //Bad
        public Task<int> BadAsync(int a, int b)
        {
            return Task.Run(() => a + b);
        }

        //Good
        public Task<int> GoodAsync(int a, int b)
        {
            return Task.FromResult(a + b);
        }

        //Better
        //使用ValueTask（结构体，值类型）而不是Task（引用类型），将避免在托管堆上分配对象
        public ValueTask<int> AddAsync(int a, int b)
        {
            return new ValueTask<int>(a + b);
        }
        #endregion

        #region>Avoid using Task.Run for long running work that blocks the thread
        /*
         * TIPS 4: Avoid using Task.Run for long running work that blocks the thread
         * 运行长时间运行的任务（如整个应用生命周期的任务），使用Task.Run将会使用线程池。
         * 线程池中的线程是为那些很快会完成或足够快以允许在合理的时间范围内重用该线程的任务而准备的。
         * 从线程池中窃取线程来长时间运行是不好的，手动生成一个新的后台线程是更好的做法。
         */
        private class QueueProcessor
        {
            private readonly BlockingCollection<Message> _messageQueue = new BlockingCollection<Message>();

            public void Enqueue(Message message)
            {
                _messageQueue.Add(message);
            }

            public void StartProcessing()
            {
                //Bad
                Task.Run(ProcessQueue);

                //Good
                var thread = new Thread(ProcessQueue)
                {
                    // This is important as it allows the process to exit while this thread is running
                    IsBackground = true
                };
                thread.Start();

                //Good
                Task.Factory.StartNew(() => {
                    ProcessQueue();
                }, creationOptions: TaskCreationOptions.LongRunning);
            }

            private void ProcessQueue()
            {
                foreach (var item in _messageQueue.GetConsumingEnumerable())
                {
                    ProcessItem(item);
                }
            }

            private void ProcessItem(Message message) { }
        }

        private class Message{ }

        #endregion

        #region>Avoid using Task.Result and Task.Wait
        /*
         * TIPS 5: Avoid using Task.Result and Task.Wait
         * 只有很少的情况可以正确使用Task.Result和Task.Wait，所以一般建议不要在你的代码中使用。
         * 
         * ⚠️同步调用异步
         *    相比同步调用真正的同步方法，使用Task.Result 或者 Task.Wait来阻塞等待异步方法执行完成是非常差的行为！这种现象称为“Sync over async”。
         *    从一个比较高的层级来看这种情况：
         *          - 一个异步方法被调用
         *          - 调用线程被阻塞等待这个异步操作完成
         *          - 当异步操作完成时，解除阻塞等待该操作的代码，这个过程发生在另外一个线程中。
         *    这样做的结果是我们需要两个线程（而不是一个线程）来完成一个同步操作。这通常将导致线程池饥饿，并导致服务中断。
         *    
         * ⚠️死锁
         *    SynchronizationContext是一个抽象类，通过它可以让应用程序模型获得一个控制异步继续执行的位置的机会。
         *    ASP.NET (non-core), WPF 以及 Windows Forms 都有一个SynchronizationContext的实现，这将导致在主线程使用Task.Wait或Task.Result时产生死锁。
         *    由于这种情况，产生了很多看似“聪明”的代码片段来阻塞等待一个Task，事实上，不存在一个好的方式可以阻塞等待一个Task完成。
         *    
         *    注：ASP.NET Core没有SynchronizationContext，并且不容易出现死锁问题。
         *    参考：https://blog.stephencleary.com/2017/03/aspnetcore-synchronization-context.html
         */

        public int DoOperationBlocking()
        {
            // Bad - 阻塞当前线程
            // CallDependencyAsync will be scheduled on the default task scheduler, 去除了死锁风险.
            // 发生异常时，此方法将会抛出AggregateException包装的原始异常
            return Task.Run(() => CallDependencyAsync()).Result;
        }

        public int DoOperationBlocking2()
        {
            // Bad - 阻塞当前线程
            // CallDependencyAsync will be scheduled on the default task scheduler, 去除了死锁风险
            return Task.Run(() => CallDependencyAsync()).GetAwaiter().GetResult();
        }

        public int DoOperationBlocking3()
        {
            // Bad - 阻塞当前线程, 并阻塞线程池中的线程
            // 发生异常时，此方法将会抛出AggregateException包装的原始异常
            return Task.Run(() => CallDependencyAsync().Result).Result;
        }

        public int DoOperationBlocking4()
        {
            // Bad - 阻塞当前线程, 并阻塞线程池中的线程
            return Task.Run(() => CallDependencyAsync().GetAwaiter().GetResult()).GetAwaiter().GetResult();
        }

        public int DoOperationBlocking5()
        {
            // Bad - 阻塞当前线程
            // Bad - 没有避免死锁的发生
            // 发生异常时，此方法将会抛出AggregateException包装的原始异常
            return CallDependencyAsync().Result;
        }

        public int DoOperationBlocking6()
        {
            // Bad - 阻塞当前线程
            // Bad - 没有避免死锁的发生
            return CallDependencyAsync().GetAwaiter().GetResult();
        }

        public int DoOperationBlocking7()
        {
            // Bad - 阻塞当前线程
            // Bad - 没有避免死锁的发生
            var task = CallDependencyAsync();
            task.Wait();
            return task.GetAwaiter().GetResult();
        }
        #endregion

        #region>CancellationToken
        //CoreWebApiDemo.ValuesController:Cancel
        #endregion

        private static Task<int> CallDependencyAsync()
        {
            return Task.FromResult(1);
        }

    }
}
