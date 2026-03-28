# Manager Question Form UI Redesign — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Redesign `QuestionForm.cshtml` into a card-sectioned layout with styled type selector, scoring cards, answer option rows with visual correct/wrong toggle, DragAndDrop two-column grid, and image drop zone.

**Architecture:** Single-file HTML/CSS/JS change to `src/Examageddon.Web/Pages/Manage/QuestionForm.cshtml`. All Razor model bindings, hidden inputs, and existing JS function signatures are preserved. New JS functions are added alongside existing ones.

**Tech Stack:** Razor Pages, Tailwind CSS (CDN/Play — arbitrary class values work at runtime), vanilla JS

---

## File Map

- **Modify:** `src/Examageddon.Web/Pages/Manage/QuestionForm.cshtml` — all changes live here; no other files touched

> **Note on tests:** This is a pure UI markup change. There are no unit tests. The "test" for each task is `dotnet build` (confirms no Razor/C# compile errors) followed by a manual browser check.

---

### Task 1: Section cards — Question text, Type selector, Scoring

**Files:**
- Modify: `src/Examageddon.Web/Pages/Manage/QuestionForm.cshtml:1-82`

Replace the top three sections (question text, type radio buttons, scoring) with section cards. Hidden radio/checkbox inputs stay for model binding; visual cards are added as `<button type="button">` elements.

- [ ] **Step 1: Replace question text div with a section card**

Replace lines 11–15 with:

```cshtml
    <!-- Section: Question text -->
    <div class="bg-arcade-card border border-arcade-border rounded-xl overflow-hidden">
        <div class="px-4 py-2.5 border-b border-arcade-border">
            <span class="text-xs font-bold tracking-widest text-arcade-muted uppercase">Question</span>
        </div>
        <div class="p-4">
            <textarea asp-for="QuestionText" rows="3" required
                class="w-full bg-arcade-dark border border-arcade-border rounded-lg px-4 py-2 text-arcade-text focus:outline-none focus:border-fire resize-y"></textarea>
        </div>
    </div>
```

- [ ] **Step 2: Replace question type radio buttons with icon cards**

Replace lines 32–57 with:

```cshtml
    <!-- Section: Question type -->
    <div class="bg-arcade-card border border-arcade-border rounded-xl overflow-hidden">
        <div class="px-4 py-2.5 border-b border-arcade-border">
            <span class="text-xs font-bold tracking-widest text-arcade-muted uppercase">Question Type</span>
        </div>
        <div class="p-4 flex gap-3">
            <input type="radio" name="QuestionType" id="qt-mc" value="@((int)QuestionType.MultipleChoice)"
                   @(Model.QuestionType == QuestionType.MultipleChoice ? "checked" : "") class="hidden" />
            <input type="radio" name="QuestionType" id="qt-stmt" value="@((int)QuestionType.Statement)"
                   @(Model.QuestionType == QuestionType.Statement ? "checked" : "") class="hidden" />
            <input type="radio" name="QuestionType" id="qt-dnd" value="@((int)QuestionType.DragAndDrop)"
                   @(Model.QuestionType == QuestionType.DragAndDrop ? "checked" : "") class="hidden" />
            @{
                string mcCard = Model.QuestionType == QuestionType.MultipleChoice
                    ? "border-fire text-fire" : "border-arcade-border text-arcade-muted hover:border-arcade-muted";
                string stmtCard = Model.QuestionType == QuestionType.Statement
                    ? "border-fire text-fire" : "border-arcade-border text-arcade-muted hover:border-arcade-muted";
                string dndCard = Model.QuestionType == QuestionType.DragAndDrop
                    ? "border-fire text-fire" : "border-arcade-border text-arcade-muted hover:border-arcade-muted";
            }
            <button type="button" id="type-card-mc"
                    onclick="selectTypeCard('@((int)QuestionType.MultipleChoice)')"
                    class="type-card flex-1 bg-arcade-dark rounded-lg border-2 px-3 py-3 text-center transition @mcCard">
                <div class="text-xl mb-1">☑</div>
                <div class="text-xs font-bold type-card-label">Multiple Choice</div>
                <div class="text-xs text-arcade-muted mt-0.5">Select one or more</div>
            </button>
            <button type="button" id="type-card-stmt"
                    onclick="selectTypeCard('@((int)QuestionType.Statement)')"
                    class="type-card flex-1 bg-arcade-dark rounded-lg border-2 px-3 py-3 text-center transition @stmtCard">
                <div class="text-xl mb-1">⊙</div>
                <div class="text-xs font-bold type-card-label">Statement</div>
                <div class="text-xs text-arcade-muted mt-0.5">True / False</div>
            </button>
            <button type="button" id="type-card-dnd"
                    onclick="selectTypeCard('@((int)QuestionType.DragAndDrop)')"
                    class="type-card flex-1 bg-arcade-dark rounded-lg border-2 px-3 py-3 text-center transition @dndCard">
                <div class="text-xl mb-1">⇄</div>
                <div class="text-xs font-bold type-card-label">Drag &amp; Drop</div>
                <div class="text-xs text-arcade-muted mt-0.5">Match pairs</div>
            </button>
        </div>
    </div>
```

- [ ] **Step 3: Replace scoring section with a section card**

Replace lines 59–82 with:

```cshtml
    <!-- Section: Scoring (MC only) -->
    <div id="allow-multiple-section"
         class="bg-arcade-card border border-arcade-border rounded-xl overflow-hidden @(Model.QuestionType != QuestionType.MultipleChoice ? "hidden" : "")">
        <div class="px-4 py-2.5 border-b border-arcade-border flex items-center justify-between">
            <span class="text-xs font-bold tracking-widest text-arcade-muted uppercase">Scoring</span>
            <label class="flex items-center gap-2 cursor-pointer select-none">
                <input type="checkbox" asp-for="AllowMultipleAnswers" id="AllowMultipleAnswers"
                       class="accent-yellow-500 w-4 h-4" onchange="toggleScoringMode(this)" />
                <span class="text-sm text-arcade-muted">Allow multiple correct answers</span>
            </label>
        </div>
        <div id="scoring-mode-section" class="p-4 flex gap-3 @(Model.AllowMultipleAnswers ? "" : "hidden")">
            <input type="radio" name="ScoringMode" id="sm-aon" value="@((int)MultiAnswerScoringMode.AllOrNothing)"
                   @(Model.ScoringMode == MultiAnswerScoringMode.AllOrNothing ? "checked" : "") class="hidden" />
            <input type="radio" name="ScoringMode" id="sm-pc" value="@((int)MultiAnswerScoringMode.PartialCredit)"
                   @(Model.ScoringMode == MultiAnswerScoringMode.PartialCredit ? "checked" : "") class="hidden" />
            @{
                string aonCard = Model.ScoringMode == MultiAnswerScoringMode.AllOrNothing
                    ? "border-green-600 bg-green-950" : "border-arcade-border bg-arcade-dark hover:border-arcade-muted";
                string aonLabel = Model.ScoringMode == MultiAnswerScoringMode.AllOrNothing
                    ? "text-green-400" : "text-arcade-muted";
                string pcCard = Model.ScoringMode == MultiAnswerScoringMode.PartialCredit
                    ? "border-green-600 bg-green-950" : "border-arcade-border bg-arcade-dark hover:border-arcade-muted";
                string pcLabel = Model.ScoringMode == MultiAnswerScoringMode.PartialCredit
                    ? "text-green-400" : "text-arcade-muted";
            }
            <button type="button" id="scoring-card-aon"
                    onclick="selectScoringCard('@((int)MultiAnswerScoringMode.AllOrNothing)')"
                    class="scoring-card flex-1 rounded-lg border-2 px-4 py-3 text-left transition @aonCard">
                <div class="text-xs font-bold scoring-card-label mb-1 @aonLabel">All or Nothing</div>
                <div class="text-xs text-arcade-muted">Full points only if every correct answer is selected</div>
            </button>
            <button type="button" id="scoring-card-pc"
                    onclick="selectScoringCard('@((int)MultiAnswerScoringMode.PartialCredit)')"
                    class="scoring-card flex-1 rounded-lg border-2 px-4 py-3 text-left transition @pcCard">
                <div class="text-xs font-bold scoring-card-label mb-1 @pcLabel">Partial Credit</div>
                <div class="text-xs text-arcade-muted">Points per correct pick, no wrong-answer penalty</div>
            </button>
        </div>
    </div>
```

- [ ] **Step 4: Build**

```bash
dotnet build src/Examageddon.slnx
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 5: Manual check**

```bash
dotnet run --project src/Examageddon.Web/Examageddon.Web.csproj
```

Navigate to `/manage/exams/1/questions/edit` (create any exam first if needed). Verify:
- Three section cards visible for question text, type, scoring
- Type icon cards render with fire border on the currently selected type
- Scoring card shows green border on the active scoring mode
- Type cards do NOT respond to clicks yet (JS not wired) — this is expected

- [ ] **Step 6: Commit**

```bash
git add src/Examageddon.Web/Pages/Manage/QuestionForm.cshtml
git commit -m "style: add section cards for question text, type selector, and scoring"
```

---

### Task 2: Answer options section card + all JS

**Files:**
- Modify: `src/Examageddon.Web/Pages/Manage/QuestionForm.cshtml:84-136` (options + save/cancel)
- Modify: `src/Examageddon.Web/Pages/Manage/QuestionForm.cshtml:138-253` (Scripts section)

Replace the options/save/cancel section and rewrite the `@section Scripts` block with all updated and new functions.

- [ ] **Step 1: Replace the answer options section and save/cancel buttons**

Replace lines 84–136 (from `<div>` containing `options-label` through the closing `</form>`) with:

```cshtml
    <!-- Section: Answer options / pairs -->
    <div class="bg-arcade-card border border-arcade-border rounded-xl overflow-hidden">
        <div class="px-4 py-2.5 border-b border-arcade-border flex items-center justify-between">
            <span class="text-xs font-bold tracking-widest text-arcade-muted uppercase" id="options-label">
                @(Model.QuestionType == QuestionType.Statement ? "Statements"
                  : Model.QuestionType == QuestionType.DragAndDrop ? "Pairs"
                  : "Answer Options")
            </span>
            <span id="mc-hint" class="text-xs text-arcade-muted @(Model.QuestionType != QuestionType.MultipleChoice ? "hidden" : "")">Check = correct</span>
        </div>
        <div class="p-4">
            <div id="dnd-headers" class="grid grid-cols-[1fr_1fr_24px] gap-3 mb-2 px-0.5 @(Model.QuestionType != QuestionType.DragAndDrop ? "hidden" : "")">
                <span class="text-xs font-bold tracking-widest text-arcade-muted uppercase">Label</span>
                <span class="text-xs font-bold tracking-widest text-arcade-muted uppercase">Sentence</span>
                <span></span>
            </div>
            <div id="options-container" class="space-y-2">
                @for (int i = 0; i < Model.OptionTexts.Count; i++)
                {
                    bool isCorrect = Model.CorrectOptionIndices.Contains(i);
                    bool isDnD = Model.QuestionType == QuestionType.DragAndDrop;
                    bool isStmt = Model.QuestionType == QuestionType.Statement;
                    string rowBase = isDnD
                        ? "option-row grid grid-cols-[1fr_1fr_24px] gap-3 items-center"
                        : "option-row flex items-center gap-3 rounded-lg px-3 py-2 border " +
                          (isCorrect ? "bg-green-950 border-green-800" : "bg-arcade-dark border-arcade-border");
                    <div class="@rowBase">
                        <input type="checkbox" name="CorrectOptionIndices" value="@i"
                               @(isCorrect ? "checked" : "") class="correct-input hidden" />
                        <button type="button" onclick="toggleCorrect(this)"
                                class="correct-toggle mc-only @(isStmt || isDnD ? "hidden" : "") w-5 h-5 rounded flex-shrink-0 flex items-center justify-center border-2 transition @(isCorrect ? "bg-green-600 border-green-600 text-white" : "border-slate-600 bg-transparent")">
                            <span class="correct-check text-xs font-bold @(isCorrect ? "" : "hidden")">✓</span>
                        </button>
                        <input type="text" name="MatchTexts[@i]"
                               value="@(Model.MatchTexts.Count > i ? Model.MatchTexts[i] : "")"
                               placeholder="Label @(i + 1)"
                               class="match-text dnd-only @(isDnD ? "" : "hidden") bg-arcade-dark border border-arcade-border rounded-lg px-3 py-2 text-arcade-text placeholder-arcade-muted focus:outline-none focus:border-fire text-sm" />
                        <input type="text" name="OptionTexts[@i]" value="@Model.OptionTexts[i]"
                               placeholder="@(isStmt ? $"Statement {i + 1}" : isDnD ? $"Sentence {i + 1}" : $"Option {i + 1}")"
                               class="option-text @(isDnD ? "bg-arcade-dark border border-arcade-border rounded-lg px-3 py-2 focus:border-fire" : "flex-1 bg-transparent border-none") @(isCorrect && !isDnD ? "text-green-300" : "text-arcade-text") placeholder-arcade-muted focus:outline-none text-sm" />
                        <div class="stmt-only @(isStmt ? "" : "hidden") flex gap-1 flex-shrink-0">
                            <button type="button" onclick="setStmtYes(this)"
                                    class="stmt-yes px-3 py-1 text-sm rounded-lg border transition @(isCorrect ? "border-fire text-fire" : "border-arcade-border text-arcade-muted")">Yes</button>
                            <button type="button" onclick="setStmtNo(this)"
                                    class="stmt-no px-3 py-1 text-sm rounded-lg border transition @(!isCorrect ? "border-fire text-fire" : "border-arcade-border text-arcade-muted")">No</button>
                        </div>
                        <button type="button" onclick="removeOption(this)"
                                class="text-arcade-muted hover:text-red-400 transition text-xl leading-none px-1 @(isDnD ? "" : "flex-shrink-0")">×</button>
                    </div>
                }
            </div>
            <button id="add-option-btn" type="button" onclick="addOption()"
                    class="mt-3 w-full px-4 py-2 text-sm border border-dashed border-arcade-border rounded-lg text-arcade-muted hover:border-fire hover:text-fire transition">
                @(Model.QuestionType == QuestionType.Statement ? "+ Add statement"
                  : Model.QuestionType == QuestionType.DragAndDrop ? "+ Add pair"
                  : "+ Add option")
            </button>
        </div>
    </div>

    <div asp-validation-summary="All" class="text-red-400 text-sm"></div>

    <button type="submit"
            class="w-full py-3 bg-gradient-to-r from-fire to-fire-hot text-slate-900 font-bold rounded-xl hover:opacity-90 transition text-base">
        @(Model.EditQuestionId.HasValue ? "Save Question" : "Add Question")
    </button>
    <a href="/manage/exams/@Model.ExamId/questions"
       class="block text-center py-2.5 bg-arcade-card border border-arcade-border rounded-xl hover:border-arcade-muted transition text-sm">
        Cancel
    </a>
</form>
```

- [ ] **Step 2: Replace the entire `@section Scripts` block**

Replace everything from line 138 (`@section Scripts {`) through line 253 (`}`) with:

```cshtml
@section Scripts {
<script>
function previewImage(input) {
    const preview = document.getElementById('img-preview');
    if (input.files && input.files[0]) {
        const reader = new FileReader();
        reader.onload = e => {
            preview.src = e.target.result;
            preview.classList.remove('hidden');
            const lbl = document.getElementById('drop-zone-label');
            if (lbl) lbl.classList.add('hidden');
        };
        reader.readAsDataURL(input.files[0]);
    }
}

function toggleScoringMode(checkbox) {
    document.getElementById('scoring-mode-section').classList.toggle('hidden', !checkbox.checked);
}

function switchQuestionType(value) {
    const isMC = value === '@((int)QuestionType.MultipleChoice)';
    const isDnD = value === '@((int)QuestionType.DragAndDrop)';
    const isStatement = value === '@((int)QuestionType.Statement)';
    document.getElementById('allow-multiple-section').classList.toggle('hidden', !isMC);
    if (isMC) {
        document.getElementById('scoring-mode-section').classList.toggle('hidden', !document.getElementById('AllowMultipleAnswers').checked);
    }
    document.querySelectorAll('.mc-only').forEach(el => el.classList.toggle('hidden', !isMC));
    document.querySelectorAll('.stmt-only').forEach(el => el.classList.toggle('hidden', !isStatement));
    document.querySelectorAll('.dnd-only').forEach(el => el.classList.toggle('hidden', !isDnD));
    document.getElementById('dnd-headers').classList.toggle('hidden', !isDnD);
    document.getElementById('mc-hint').classList.toggle('hidden', !isMC);
    document.getElementById('options-label').textContent =
        isStatement ? 'Statements' : isDnD ? 'Pairs' : 'Answer Options';
    document.getElementById('add-option-btn').textContent =
        isStatement ? '+ Add statement' : isDnD ? '+ Add pair' : '+ Add option';
    document.querySelectorAll('.option-text').forEach((el, i) => {
        el.placeholder = isStatement ? 'Statement ' + (i + 1) : isDnD ? 'Sentence ' + (i + 1) : 'Option ' + (i + 1);
    });
    document.querySelectorAll('.match-text').forEach((el, i) => { el.placeholder = 'Label ' + (i + 1); });
}

function reindexOptions() {
    const typeVal = document.querySelector('input[name="QuestionType"]:checked')?.value;
    const isStatement = typeVal === '@((int)QuestionType.Statement)';
    const isDnD = typeVal === '@((int)QuestionType.DragAndDrop)';
    document.querySelectorAll('#options-container .option-row').forEach((row, i) => {
        row.querySelector('.correct-input').value = i;
        const text = row.querySelector('.option-text');
        text.name = `OptionTexts[${i}]`;
        text.placeholder = isStatement ? 'Statement ' + (i + 1) : isDnD ? 'Sentence ' + (i + 1) : 'Option ' + (i + 1);
        const match = row.querySelector('.match-text');
        if (match) { match.name = `MatchTexts[${i}]`; match.placeholder = 'Label ' + (i + 1); }
    });
}

function addOption() {
    const container = document.getElementById('options-container');
    const i = container.querySelectorAll('.option-row').length;
    const typeVal = document.querySelector('input[name="QuestionType"]:checked')?.value;
    const isStatement = typeVal === '@((int)QuestionType.Statement)';
    const isDnD = typeVal === '@((int)QuestionType.DragAndDrop)';
    const div = document.createElement('div');
    if (isDnD) {
        div.className = 'option-row grid grid-cols-[1fr_1fr_24px] gap-3 items-center';
        div.innerHTML = `
            <input type="checkbox" name="CorrectOptionIndices" value="${i}" class="correct-input hidden" />
            <button type="button" onclick="toggleCorrect(this)" class="correct-toggle mc-only hidden w-5 h-5 rounded flex-shrink-0 flex items-center justify-center border-2 border-slate-600 bg-transparent transition"><span class="correct-check text-xs font-bold hidden">✓</span></button>
            <input type="text" name="MatchTexts[${i}]" placeholder="Label ${i + 1}" class="match-text dnd-only bg-arcade-dark border border-arcade-border rounded-lg px-3 py-2 text-arcade-text placeholder-arcade-muted focus:outline-none focus:border-fire text-sm" />
            <input type="text" name="OptionTexts[${i}]" placeholder="Sentence ${i + 1}" class="option-text bg-arcade-dark border border-arcade-border rounded-lg px-3 py-2 text-arcade-text placeholder-arcade-muted focus:outline-none focus:border-fire text-sm" />
            <div class="stmt-only hidden flex gap-1 flex-shrink-0"><button type="button" onclick="setStmtYes(this)" class="stmt-yes px-3 py-1 text-sm rounded-lg border border-arcade-border text-arcade-muted transition">Yes</button><button type="button" onclick="setStmtNo(this)" class="stmt-no px-3 py-1 text-sm rounded-lg border border-fire text-fire transition">No</button></div>
            <button type="button" onclick="removeOption(this)" class="text-arcade-muted hover:text-red-400 transition text-xl leading-none px-1">×</button>`;
    } else {
        div.className = 'option-row flex items-center gap-3 rounded-lg px-3 py-2 border bg-arcade-dark border-arcade-border';
        div.innerHTML = `
            <input type="checkbox" name="CorrectOptionIndices" value="${i}" class="correct-input hidden" />
            <button type="button" onclick="toggleCorrect(this)" class="correct-toggle mc-only${isStatement ? ' hidden' : ''} w-5 h-5 rounded flex-shrink-0 flex items-center justify-center border-2 border-slate-600 bg-transparent transition"><span class="correct-check text-xs font-bold hidden">✓</span></button>
            <input type="text" name="MatchTexts[${i}]" placeholder="Label ${i + 1}" class="match-text dnd-only hidden bg-arcade-dark border border-arcade-border rounded-lg px-3 py-2 text-arcade-text placeholder-arcade-muted focus:outline-none focus:border-fire text-sm" />
            <input type="text" name="OptionTexts[${i}]" placeholder="${isStatement ? 'Statement' : 'Option'} ${i + 1}" class="option-text flex-1 bg-transparent border-none text-arcade-text placeholder-arcade-muted focus:outline-none text-sm" />
            <div class="stmt-only${isStatement ? '' : ' hidden'} flex gap-1 flex-shrink-0"><button type="button" onclick="setStmtYes(this)" class="stmt-yes px-3 py-1 text-sm rounded-lg border border-arcade-border text-arcade-muted transition">Yes</button><button type="button" onclick="setStmtNo(this)" class="stmt-no px-3 py-1 text-sm rounded-lg border border-fire text-fire transition">No</button></div>
            <button type="button" onclick="removeOption(this)" class="flex-shrink-0 text-arcade-muted hover:text-red-400 transition text-xl leading-none px-1">×</button>`;
    }
    container.appendChild(div);
}

function removeOption(btn) { btn.closest('.option-row').remove(); reindexOptions(); }

function setStmtYes(btn) {
    const row = btn.closest('.option-row');
    row.querySelector('.correct-input').checked = true;
    row.querySelector('.stmt-yes').classList.add('border-fire', 'text-fire');
    row.querySelector('.stmt-yes').classList.remove('border-arcade-border', 'text-arcade-muted');
    row.querySelector('.stmt-no').classList.remove('border-fire', 'text-fire');
    row.querySelector('.stmt-no').classList.add('border-arcade-border', 'text-arcade-muted');
}

function setStmtNo(btn) {
    const row = btn.closest('.option-row');
    row.querySelector('.correct-input').checked = false;
    row.querySelector('.stmt-no').classList.add('border-fire', 'text-fire');
    row.querySelector('.stmt-no').classList.remove('border-arcade-border', 'text-arcade-muted');
    row.querySelector('.stmt-yes').classList.remove('border-fire', 'text-fire');
    row.querySelector('.stmt-yes').classList.add('border-arcade-border', 'text-arcade-muted');
}

function selectTypeCard(value) {
    const mcVal = '@((int)QuestionType.MultipleChoice)';
    const stmtVal = '@((int)QuestionType.Statement)';
    const dndVal = '@((int)QuestionType.DragAndDrop)';
    document.querySelectorAll('input[name="QuestionType"]').forEach(r => { r.checked = (r.value === String(value)); });
    [{ id: 'type-card-mc', val: mcVal }, { id: 'type-card-stmt', val: stmtVal }, { id: 'type-card-dnd', val: dndVal }]
        .forEach(({ id, val }) => {
            const card = document.getElementById(id);
            const active = val === String(value);
            card.classList.toggle('border-fire', active);
            card.classList.toggle('text-fire', active);
            card.classList.toggle('border-arcade-border', !active);
            card.classList.toggle('text-arcade-muted', !active);
        });
    switchQuestionType(String(value));
}

function selectScoringCard(value) {
    document.querySelectorAll('input[name="ScoringMode"]').forEach(r => { r.checked = (r.value === String(value)); });
    const aonActive = value === '@((int)MultiAnswerScoringMode.AllOrNothing)';
    [{ id: 'scoring-card-aon', active: aonActive }, { id: 'scoring-card-pc', active: !aonActive }]
        .forEach(({ id, active }) => {
            const card = document.getElementById(id);
            card.classList.toggle('border-green-600', active);
            card.classList.toggle('bg-green-950', active);
            card.classList.toggle('border-arcade-border', !active);
            card.classList.toggle('bg-arcade-dark', !active);
            const lbl = card.querySelector('.scoring-card-label');
            lbl.classList.toggle('text-green-400', active);
            lbl.classList.toggle('text-arcade-muted', !active);
        });
}

function toggleCorrect(btn) {
    const row = btn.closest('.option-row');
    const cb = row.querySelector('.correct-input');
    cb.checked = !cb.checked;
    const isCorrect = cb.checked;
    row.classList.toggle('bg-green-950', isCorrect);
    row.classList.toggle('border-green-800', isCorrect);
    row.classList.toggle('bg-arcade-dark', !isCorrect);
    row.classList.toggle('border-arcade-border', !isCorrect);
    btn.classList.toggle('bg-green-600', isCorrect);
    btn.classList.toggle('border-green-600', isCorrect);
    btn.classList.toggle('text-white', isCorrect);
    btn.classList.toggle('border-slate-600', !isCorrect);
    btn.classList.toggle('bg-transparent', !isCorrect);
    const check = btn.querySelector('.correct-check');
    if (check) check.classList.toggle('hidden', !isCorrect);
    const textInput = row.querySelector('.option-text');
    if (textInput) {
        textInput.classList.toggle('text-green-300', isCorrect);
        textInput.classList.toggle('text-arcade-text', !isCorrect);
    }
}
</script>
}
```

- [ ] **Step 3: Build**

```bash
dotnet build src/Examageddon.slnx
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Manual check**

```bash
dotnet run --project src/Examageddon.Web/Examageddon.Web.csproj
```

Navigate to `/manage/exams/1/questions/edit`. Verify:
- Clicking a type card switches the active card (fire border) and updates the options section
- MC option rows: clicking the square toggle turns the row green and shows ✓
- Clicking scoring cards switches the active card (green border)
- DragAndDrop mode shows two equal-width columns with "Label" / "Sentence" headers
- Adding options generates correctly styled rows
- Form submits and saves correctly (create a question, re-open it, verify values round-trip)

- [ ] **Step 5: Commit**

```bash
git add src/Examageddon.Web/Pages/Manage/QuestionForm.cshtml
git commit -m "style: add answer options section card, styled correct toggle, DnD grid layout, and updated JS"
```

---

### Task 3: Image drop zone

**Files:**
- Modify: `src/Examageddon.Web/Pages/Manage/QuestionForm.cshtml` — image section only

The image upload currently uses a raw unstyled `<input type="file">`. Replace it with a styled drop zone. The hidden file input stays for model binding; the drop zone triggers it on click and accepts drag-and-drop.

- [ ] **Step 1: Remove the original image section**

After Tasks 1 and 2, the file's form section order is:
1. Question text card
2. **Original image div** ← delete this entire block
3. Type selector card
4. Scoring card
5. Answer options card
6. Save/Cancel buttons

Find and delete the `<div>` block that starts with `<label class="block text-sm font-bold text-arcade-muted mb-2">Image (optional)</label>`. It looks like:

```cshtml
    <div>
        <label class="block text-sm font-bold text-arcade-muted mb-2">Image (optional)</label>
        @if (Model.ExistingQuestion?.ImageData != null)
        { ... }
        else
        { ... }
        <input type="file" asp-for="Image" accept="image/*" ... />
    </div>
```

Delete that whole block (do not replace it yet).

- [ ] **Step 2: Insert the new image section card between scoring card and answer options card**

In the gap between the closing `</div>` of the scoring card (`allow-multiple-section`) and the opening `<div class="bg-arcade-card ...">` of the answer options section, insert:

```cshtml
    <!-- Section: Image -->
    <div class="bg-arcade-card border border-arcade-border rounded-xl overflow-hidden">
        <div class="px-4 py-2.5 border-b border-arcade-border">
            <span class="text-xs font-bold tracking-widest text-arcade-muted uppercase">Image</span>
            <span class="text-xs text-arcade-muted ml-1">(optional)</span>
        </div>
        <div class="p-4">
            <input type="file" asp-for="Image" accept="image/*" id="file-input" class="hidden" onchange="previewImage(this)" />
            @if (Model.ExistingQuestion?.ImageData != null)
            {
                <div id="drop-zone" onclick="document.getElementById('file-input').click()"
                     class="border-2 border-dashed border-arcade-border rounded-lg cursor-pointer hover:border-fire transition text-center">
                    <img src="/images/question/@Model.ExistingQuestion.Id" id="img-preview"
                         class="rounded-lg max-h-40 object-contain mx-auto p-2" />
                    <p class="text-xs text-arcade-muted pb-3">Click to replace</p>
                </div>
            }
            else
            {
                <div id="drop-zone" onclick="document.getElementById('file-input').click()"
                     class="border-2 border-dashed border-arcade-border rounded-lg p-7 text-center cursor-pointer hover:border-fire transition">
                    <img id="img-preview" class="hidden rounded-lg max-h-40 object-contain mx-auto mb-2" />
                    <div id="drop-zone-label">
                        <div class="text-2xl mb-1">📎</div>
                        <p class="text-sm text-arcade-muted">Drop image here or <span class="text-fire underline">browse</span></p>
                        <p class="text-xs text-arcade-muted mt-1">PNG, JPG</p>
                    </div>
                </div>
            }
        </div>
    </div>
```

- [ ] **Step 3: Add drop zone JS at the end of the script block**

Inside the `<script>` tag (after the `toggleCorrect` function, before the closing `</script>`), add:

```js
(function () {
    const zone = document.getElementById('drop-zone');
    const fileInput = document.getElementById('file-input');
    if (!zone || !fileInput) return;
    zone.addEventListener('dragover', e => { e.preventDefault(); zone.classList.add('border-fire'); });
    zone.addEventListener('dragleave', () => zone.classList.remove('border-fire'));
    zone.addEventListener('drop', e => {
        e.preventDefault();
        zone.classList.remove('border-fire');
        const file = e.dataTransfer.files[0];
        if (file && file.type.startsWith('image/')) {
            const dt = new DataTransfer();
            dt.items.add(file);
            fileInput.files = dt.files;
            previewImage(fileInput);
        }
    });
}());
```

- [ ] **Step 4: Build**

```bash
dotnet build src/Examageddon.slnx
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 5: Manual check**

Navigate to a question edit form. Verify:
- Image section shows a dashed drop zone with 📎 icon
- Clicking anywhere on the drop zone opens the file picker
- Dragging an image file onto the drop zone shows the image preview and hides the label text
- Editing a question that already has an image shows the image with "Click to replace" text
- Submitting with a new image saves correctly

- [ ] **Step 6: Commit**

```bash
git add src/Examageddon.Web/Pages/Manage/QuestionForm.cshtml
git commit -m "style: replace image file input with styled drop zone"
```
