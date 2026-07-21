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
    [ProducesResponseType(typeof(StudentViewModel), StatusCodes.Status200OK)]
    public async Task<ActionResult<StudentViewModel>> GetStudents(
        CancellationToken cancellationToken)
    {
        var students = (await _studentRepository.GetAllAsync(cancellationToken));

        return Ok(students);
    }
}