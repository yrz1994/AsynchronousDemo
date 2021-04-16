using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading;
using System.Threading.Tasks;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace WebApiDemo.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ValuesController : ControllerBase
    {
        #region>Async void
        /*
         * TIPS 2: Async void
         * 在ASP.NET Core中使用async void始终是不好的，Never Do It.
         * 通常它被用来通过触发Controller中的Action来实现“Fire and forget”模式
         * 当遇到异常被抛出时，Async void方法将会导致进程崩溃
         */

        [HttpPost("/bad")]
        public IActionResult Bad()
        {
            BackgroundOperationAsyncVoid();
            return Accepted();
        }
        
        [HttpPost("/good")]
        public IActionResult Good()
        {
            Task.Run(BackgroundOperationAsync);
            return Accepted();
        }

        private async void BackgroundOperationAsyncVoid()
        {
            await CallDependencyAsync();
            return;
        }

        private async Task BackgroundOperationAsync()
        {
            await CallDependencyAsync();
            return;
        }

        private Task<int> CallDependencyAsync()
        {
            throw new Exception();
            return Task.FromResult(1);
        }
        #endregion

        #region>同步调用异步
        /// <summary>
        /// Bad 但不会死锁
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        [HttpGet("/bad")]
        public int Bad(int input)
        {
            var result = Calculate(input).GetAwaiter().GetResult();
            return result;
        }

        /// <summary>
        /// Good
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        [HttpGet("/good")]
        public async Task<int> Good(int input)
        {
            var result = await Calculate(input);
            return result;
        }

        private async Task<int> Calculate(int input)
        {
            var result = await Task.Run(() => {
                var output = input;
                for (var i = 0; i < 100000; i++)
                {
                    output++;
                }
                return output;
            });
            return result;
        }
        #endregion

        #region>CancellationTokens

        [HttpGet("/Cancell")]
        public async Task<int> Cancel(int input, CancellationToken cancellationToken)
        {
            try
            {
                var result = await CalculateSometing(input, cancellationToken);
                return result;
            }
            catch(OperationCanceledException e)
            {
                return 0;
            }
        }

        private async Task<int> CalculateSometing(int input, CancellationToken cancellationToken)
        {
            var result = await Task.Run(async () => {
                var output = input;
                for (var i = 0; i < 15; i++)
                {
                    output++;
                    await Task.Delay(1000, cancellationToken);
                }
                return output;
            }, cancellationToken);
            return result;
        }

        #endregion
    }
}
