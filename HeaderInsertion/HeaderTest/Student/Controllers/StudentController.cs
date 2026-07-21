using Microsoft.AspNetCore.Mvc;

namespace Student.Controllers;

[ApiController]
[Route("api/students")]
public sealed class StudentController : ControllerBase
{
    private readonly IStudentRepository _studentRepository;

    public StudentController(IStudentRepository studentRepository)
    {
        _studentRepository = studentRepository;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<global::Customer.Student>), StatusCodes.Status200OK)]
    public async Task<ActionResult<global::Customer.Student>> GetStudents(
        CancellationToken cancellationToken)
    {
        var students = (await _studentRepository.GetAllAsync(cancellationToken));

        return Ok(students);
    }
}