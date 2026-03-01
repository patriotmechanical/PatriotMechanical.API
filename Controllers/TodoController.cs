using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PatriotMechanical.API.Domain.Entities;
using PatriotMechanical.API.Infrastructure.Data;

namespace PatriotMechanical.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("todos")]
    public class TodoController : ControllerBase
    {
        private readonly AppDbContext _context;

        public TodoController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var isDemo = DemoFilter.IsDemo(User);
            var todos = await _context.TodoItems
                .Where(t => !isDemo || t.IsDemo)
                .Where(t => isDemo || !t.IsDemo)
                .OrderBy(t => t.IsCompleted)
                .ThenBy(t => t.SortOrder)
                .ThenByDescending(t => t.CreatedAt)
                .Select(t => new
                {
                    t.Id, t.Title, t.Description, t.IsCompleted,
                    t.CreatedAt, t.CompletedAt
                })
                .ToListAsync();

            return Ok(todos);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateTodoRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Title))
                return BadRequest(new { message = "Title is required." });

            var isDemo = DemoFilter.IsDemo(User);
            var maxOrder = await _context.TodoItems
                .Where(t => !isDemo || t.IsDemo)
                .Where(t => isDemo || !t.IsDemo)
                .MaxAsync(t => (int?)t.SortOrder) ?? -1;

            var todo = new TodoItem
            {
                Id = Guid.NewGuid(),
                Title = req.Title.Trim(),
                Description = req.Description?.Trim(),
                IsDemo = isDemo,
                SortOrder = maxOrder + 1
            };

            _context.TodoItems.Add(todo);
            await _context.SaveChangesAsync();

            return Ok(new { todo.Id, todo.Title, todo.Description, todo.IsCompleted, todo.CreatedAt });
        }

        [HttpPut("{id}/toggle")]
        public async Task<IActionResult> Toggle(Guid id)
        {
            var todo = await _context.TodoItems.FindAsync(id);
            if (todo == null) return NotFound();

            todo.IsCompleted = !todo.IsCompleted;
            todo.CompletedAt = todo.IsCompleted ? DateTime.UtcNow : null;
            await _context.SaveChangesAsync();

            return Ok(new { todo.Id, todo.IsCompleted, todo.CompletedAt });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] CreateTodoRequest req)
        {
            var todo = await _context.TodoItems.FindAsync(id);
            if (todo == null) return NotFound();

            if (req.Title != null) todo.Title = req.Title.Trim();
            if (req.Description != null) todo.Description = req.Description.Trim();
            await _context.SaveChangesAsync();

            return Ok(new { todo.Id, todo.Title, todo.Description });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var todo = await _context.TodoItems.FindAsync(id);
            if (todo == null) return NotFound();

            _context.TodoItems.Remove(todo);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Deleted." });
        }
    }

    public class CreateTodoRequest
    {
        public string Title { get; set; } = null!;
        public string? Description { get; set; }
    }
}