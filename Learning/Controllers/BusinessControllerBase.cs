using LearningBackendAPI.Helpers;
using Microsoft.AspNetCore.Mvc;

namespace LearningBackendAPI.Controllers
{
    /// <summary>
    /// Turns the business exceptions the services throw into the same error responses the other
    /// controllers return: KeyNotFoundException → 404, InvalidOperationException → 400, both wrapped
    /// in the standard ApiResponse via ResponseHelper.
    /// </summary>
    public abstract class BusinessControllerBase : ControllerBase
    {
        protected readonly ResponseHelper _responseHelper;

        protected BusinessControllerBase(ResponseHelper responseHelper)
        {
            _responseHelper = responseHelper;
        }

        protected async Task<IActionResult> HandleAsync(Func<Task<IActionResult>> action)
        {
            try
            {
                return await action();
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(_responseHelper.NotFound<object>(ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(_responseHelper.BadRequest<object>(ex.Message));
            }
        }
    }
}
