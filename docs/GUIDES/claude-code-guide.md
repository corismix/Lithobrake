# Claude Code Usage Guide for Lithobrake

This guide explains how to use Claude Code effectively for implementing Lithobrake as a non-coder.

## Quick Start Workflow

### Before Each Session

1. **Check your current task**: Open `docs/IMPLEMENTATION/current-task.md`
2. **Start Claude Code**: Open the project in Claude Code
3. **Begin with**: "Please implement the current task in docs/IMPLEMENTATION/current-task.md"

### During Each Session

Use these exact commands for best results:

#### Implementation Commands
- **"Please implement the current task in docs/IMPLEMENTATION/current-task.md"**
  - Claude will read the task and implement it step by step
- **"Show me what files were created or modified"**
  - Lists all changes made during implementation
- **"Run the project and verify it works"**
  - Compiles and runs the project to test functionality

#### Validation Commands  
- **"Show me the performance metrics"**
  - Displays frame timing and performance data
- **"Verify this meets the requirements listed in the task"**
  - Checks implementation against success criteria
- **"Check if the implementation passes the success criteria"**
  - Validates all task requirements are met

#### Debugging Commands
- **"The project won't compile - what's wrong?"**
  - Helps fix compilation errors
- **"Performance is below target - how can we optimize?"**
  - Analyzes and improves performance issues
- **"Run the tests described in the task"**
  - Executes specific validation tests

### After Task Completion

Claude Code will automatically:
1. **Validate everything works**: Run all validation steps for the task
2. **Record progress**: Update `docs/IMPLEMENTATION/completed-tasks.md` with completion details
3. **Prepare next task**: Update `docs/IMPLEMENTATION/current-task.md` with the next task
4. **Report next steps**: Tell you what the next task will be

You just need to start the next session with: "Please implement the current task"

## Document Structure Reference

### Your Key Documents
- **`CLAUDE.md`**: Always loaded by Claude Code - contains project context
- **`docs/IMPLEMENTATION/current-task.md`**: Your active work focus - update before each session
- **`docs/IMPLEMENTATION/tasks.md`**: Master task list - your roadmap
- **`docs/IMPLEMENTATION/completed-tasks.md`**: Progress log - track what's done

### Reference Documents (Use When Needed)
- **`docs/tasks-og.md`**: Detailed technical notes and code snippets  
- **`docs/requirements.md`**: Acceptance criteria for features
- **`docs/design.md`**: System architecture and design decisions
- **`docs/determinism.md`**: Simulation consistency requirements

## Task Management Process

### 1. Automated Task Management

Claude Code handles task preparation automatically:

1. **Finds next task**: Automatically checks `docs/IMPLEMENTATION/tasks.md`
2. **Updates current-task.md**: 
   - Copies task description from tasks.md
   - Adds technical details from tasks-og.md when needed
   - Includes specific files to create/modify
   - Lists clear success criteria
3. **You just start the session**: "Please implement the current task"

### 2. During Implementation

- Let Claude Code read current-task.md and implement
- Ask for performance checks frequently
- Verify each step meets the requirements
- Don't hesitate to ask "Why did you do it that way?" if curious

### 3. Automatic Completion

Claude Code will automatically:

- ✅ Validate code compiles without errors
- ✅ Verify project runs without crashes  
- ✅ Check performance targets met (physics <5ms, scripts <3ms)
- ✅ Confirm all success criteria from task are met
- ✅ Test no regression in previously working features
- ✅ Update `completed-tasks.md` with completion details
- ✅ Prepare the next task in `current-task.md`
- ✅ Report what's coming next

## Common Scenarios

### "I don't understand what this task is asking for"
1. Check `docs/tasks-og.md` for detailed technical explanation
2. Ask Claude: "Explain what this task is trying to accomplish"
3. Look at the requirements in `docs/requirements.md` for context

### "The implementation seems wrong"
1. Ask: "Does this implementation meet the success criteria?"
2. Ask: "Show me how this compares to the requirements"
3. Reference the technical notes in tasks-og.md

### "Performance is below target"
1. Ask: "Show me the current performance metrics"
2. Ask: "What's causing the performance issue?"
3. Ask: "How can we optimize this to meet the targets?"

### "I want to see the big picture"
1. Check `docs/IMPLEMENTATION/tasks.md` for full roadmap
2. Check `docs/design.md` for system architecture
3. Ask: "Show me how this task fits into the overall project"

## Performance Monitoring

Always check these after each task:
- **Frame rate**: Must stay at 60 FPS
- **Physics time**: Must be ≤5ms per frame  
- **Script time**: Must be ≤3ms per frame
- **Total frame time**: Must be ≤16.6ms

Commands:
- "Show me the current performance numbers"
- "Is this meeting the performance targets?"
- "Run the 75-part stress test"

## Troubleshooting

### Claude Code seems confused
- Make sure current-task.md is clear and specific
- Try: "Please re-read the current task and start over"
- Check that CLAUDE.md hasn't been corrupted

### Task seems too complex  
- Break it into smaller subtasks in current-task.md
- Ask: "Can we implement this step by step?"
- Reference tasks-og.md for detailed technical approach

### Not sure if task is complete
- Ask: "Have we met all the success criteria for this task?"
- Ask: "Run all the validation steps"
- Check performance numbers against targets

## Success Tips

1. **Be specific**: Use the exact commands listed in this guide
2. **Check frequently**: Ask for performance metrics often
3. **Validate thoroughly**: Don't rush to the next task
4. **Keep records**: Update completed-tasks.md religiously  
5. **Ask questions**: Claude Code can explain anything you don't understand

Remember: You don't need to understand the code - you just need to follow the process and validate that requirements are met!