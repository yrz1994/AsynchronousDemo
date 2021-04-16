using System.Threading.Tasks;
using System.Web.Http;

namespace FrameworkWebApiDemo.Controllers
{
    public class ValuesController : ApiController
    {
        /// <summary>
        /// BAD!
        /// 发生死锁
        /// /api/Values/Bad?input=1
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        [HttpGet]
        public int Bad(int input)
        {
            var result = Calculate(input).GetAwaiter().GetResult();
            return result;
        }

        /// <summary>
        /// BAD!
        /// 链路中每个await都加.ConfigureAwait(false)避免死锁
        /// /api/Values/Bad2?input=1
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        [HttpGet]
        public int Bad2(int input)
        {
            var result = CalculateWithConfigAwait(input).GetAwaiter().GetResult();
            return result;
        }

        /// <summary>
        /// Good
        /// /api/Values/Bad?input=1
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        [HttpGet]
        public async Task<int> Good(int input)
        {
            var result = await Calculate(input);
            return result;
        }

        private async Task<int> Calculate(int input)
        {
            var result = await Task.Run(() =>{
                var output = input;
                for (var i = 0; i < 100000; i++)
                {
                    output++;
                }
                return output;
            });
            return result;
        }

        private async Task<int> CalculateWithConfigAwait(int input)
        {
            var result = await Task.Run(() => {
                var output = input;
                for (var i = 0; i < 100000; i++)
                {
                    output++;
                }
                return output;
            }).ConfigureAwait(false);
            return result;
        }
    }
}
