import {
  Board,
  BoardContactPhase,
  BoardContactType,
  type BoardContact,
  type BoardPauseResult,
  type BoardPlayer,
} from "@harrishill/board-sdk";

const MIN_PLAYERS = 3;
const MAX_PLAYERS = 6;
const PROMPT_SECONDS = 75;
const ROUND_VOTE_SECONDS = 35;
const FINAL_VOTE_SECONDS = 40;
const GAME_VERSION = "0.1.0-web";

const PLAYER_COLORS = [
  "#f35f6f",
  "#31a9ee",
  "#37c978",
  "#f5ba3d",
  "#a780f0",
  "#ff884d",
  "#35d8c7",
  "#f06bb4",
];

type GamePhase = "title" | "join" | "prompt" | "vote" | "results" | "scoreboard" | "winner";
type PlayerKind = BoardPlayer["type"] | "profile" | "guest";

interface PromptEntry {
  id: string;
  text: string;
  type: string;
}

interface PromptPackData {
  packName: string;
  prompts: PromptEntry[];
  finalPrompts: PromptEntry[];
  randomAnswers: string[];
}

interface PlayerData {
  id: number;
  seatIndex: number;
  displayName: string;
  color: string;
  score: number;
  boardSessionId?: number;
  boardPlayerId?: string;
  boardPlayerType?: PlayerKind;
  avatarId?: string;
  nameInk?: HandwritingAnswer;
  nameInkConfirmed?: boolean;
}

interface InkPoint {
  x: number;
  y: number;
}

interface HandwritingStroke {
  points: InkPoint[];
}

interface HandwritingAnswer {
  text: string;
  strokes: HandwritingStroke[];
}

interface AnswerSlot {
  prompt: PromptEntry;
  playerId: number;
  answer: HandwritingAnswer;
  submitted: boolean;
  votes: number;
}

interface ScoreSummary {
  lines: string[];
}

const blankAnswer = (): HandwritingAnswer => ({ text: "", strokes: [] });

const fallbackPromptPack: PromptPackData = {
  packName: "Table Laughs Starter Pack",
  prompts: [
    {
      id: "fallback_standard_1",
      type: "fill_blank",
      text: "The school principal banned the mascot after it kept yelling, \"_____!\"",
    },
    {
      id: "fallback_standard_2",
      type: "funniest_wins",
      text: "Name the least helpful superhero power.",
    },
    {
      id: "fallback_standard_3",
      type: "fill_blank",
      text: "The treasure map led straight to _____.",
    },
    {
      id: "fallback_standard_4",
      type: "funniest_wins",
      text: "What is a terrible slogan for a sandwich shop?",
    },
  ],
  finalPrompts: [
    {
      id: "fallback_final_1",
      type: "final",
      text: "Write the punchline to the most dramatic family dinner ever.",
    },
    {
      id: "fallback_final_2",
      type: "final",
      text: "Invent the title of a movie that should never be made.",
    },
  ],
  randomAnswers: [
    "A very confident sandwich",
    "Grandma's backup kazoo",
    "The left shoe's revenge",
    "A trophy for almost trying",
  ],
};

void bootstrap();

async function bootstrap(): Promise<void> {
  const promptPack = await loadPromptPack();
  const root = document.getElementById("app");
  if (!root) {
    throw new Error("Missing #app root element.");
  }

  new TableLaughsGame(root, promptPack);
}

async function loadPromptPack(): Promise<PromptPackData> {
  try {
    const response = await fetch("./prompts/table_laughs_prompts.json", { cache: "no-cache" });
    if (!response.ok) {
      throw new Error(`HTTP ${response.status}`);
    }

    const parsed = (await response.json()) as Partial<PromptPackData>;
    if (!parsed.prompts?.length || !parsed.finalPrompts?.length) {
      throw new Error("Prompt pack has no playable prompts.");
    }

    return {
      packName: parsed.packName || fallbackPromptPack.packName,
      prompts: parsed.prompts,
      finalPrompts: parsed.finalPrompts,
      randomAnswers: parsed.randomAnswers?.length
        ? parsed.randomAnswers
        : fallbackPromptPack.randomAnswers,
    };
  } catch (error) {
    console.warn("Using fallback prompts.", error);
    return fallbackPromptPack;
  }
}

class TableLaughsGame {
  private readonly root: HTMLElement;
  private readonly promptPack: PromptPackData;
  private readonly players: PlayerData[] = [];
  private readonly standardBag: PromptEntry[] = [];
  private readonly finalBag: PromptEntry[] = [];
  private readonly draftAnswers = new Map<number, HandwritingAnswer>();
  private readonly votedPlayerIds = new Set<number>();
  private readonly inkPads = new Map<number, InkPad>();
  private readonly nameInkPads = new Map<number, InkPad>();
  private readonly startedAt = Date.now();

  private currentRound = 1;
  private currentAnswers: AnswerSlot[] = [];
  private phase: GamePhase = "title";
  private nextPlayerId = 1;
  private countdownEl: HTMLElement | null = null;
  private timerId: number | null = null;
  private profileSwitcherSyncTimerId: number | null = null;
  private timerEndsAt = 0;
  private saveId: string | null = null;
  private advancing = false;
  private contactSummary = "no contacts";

  constructor(root: HTMLElement, promptPack: PromptPackData) {
    this.root = root;
    this.promptPack = promptPack;
    this.resetPromptBags();
    this.configureBoard();
    this.showTitle();
  }

  private configureBoard(): void {
    if (!Board.isOnDevice) {
      document.body.classList.add("off-device");
      return;
    }

    document.body.classList.add("on-device");

    try {
      Board.pause.setContext({
        gameName: "Table Laughs",
        offerSaveOption: true,
        customButtons: [{ id: "restart", title: "Restart", icon: "circulararrow" }],
        audioTracks: [
          { id: "music", name: "Music", value: 75 },
          { id: "sfx", name: "Sound effects", value: 90 },
        ],
      });

      Board.input.subscribe((contacts) => this.handleBoardContacts(contacts));
      window.setInterval(() => this.pollPauseMenu(), 500);
      this.importBoardSessionPlayers();
    } catch (error) {
      console.warn("Board setup failed.", error);
    }
  }

  private handleBoardContacts(contacts: ReadonlyArray<BoardContact>): void {
    const active = contacts.filter(
      (contact) =>
        contact.phase !== BoardContactPhase.Ended &&
        contact.phase !== BoardContactPhase.Canceled,
    );
    const fingers = active.filter((contact) => contact.type === BoardContactType.Finger).length;
    const pieces = active.filter((contact) => contact.type === BoardContactType.Glyph).length;
    this.contactSummary = `${fingers} touch${fingers === 1 ? "" : "es"} / ${pieces} piece${
      pieces === 1 ? "" : "s"
    }`;

    const readout = document.getElementById("contact-readout");
    if (readout) {
      readout.textContent = this.contactSummary;
    }
  }

  private pollPauseMenu(): void {
    let result: BoardPauseResult | null = null;
    try {
      result = Board.pause.pollResult();
    } catch (error) {
      console.warn("Pause result polling failed.", error);
    }

    if (!result) {
      return;
    }

    if (result.audioTracks?.length) {
      console.info("Pause audio values", result.audioTracks);
    }

    if (result.action === "save_and_quit") {
      void this.saveSnapshot().finally(() => this.showTitle());
      return;
    }

    if (result.action === "quit") {
      this.showTitle();
      return;
    }

    if (result.action === "custom_button" && result.customButtonId === "restart") {
      this.beginJoin();
    }
  }

  private importBoardSessionPlayers(preferredSeatIndex?: number): boolean {
    if (!Board.isOnDevice) {
      return false;
    }

    const boardPlayers = this.readBoardSessionPlayers();
    if (!boardPlayers) {
      return false;
    }

    let changed = false;
    for (const boardPlayer of boardPlayers) {
      const imported = this.importBoardPlayer(boardPlayer, preferredSeatIndex);
      if (!imported && !this.findBoardPlayer(boardPlayer)) {
        return changed;
      }
      changed = changed || imported;
    }

    return changed;
  }

  private importBoardPlayer(boardPlayer: BoardPlayer, preferredSeatIndex?: number): boolean {
    const existing = this.findBoardPlayer(boardPlayer);
    if (existing) {
      return this.updateBoardPlayer(existing, boardPlayer);
    }

    const seatIndex = this.firstOpenSeat(preferredSeatIndex);
    if (seatIndex < 0) {
      return false;
    }

    const id = this.nextPlayerId;
    this.players.push({
      id,
      seatIndex,
      displayName: this.nameFromBoardPlayer(boardPlayer, id),
      color: PLAYER_COLORS[(id - 1) % PLAYER_COLORS.length],
      score: 0,
      boardSessionId: boardPlayer.sessionId,
      boardPlayerId: boardPlayer.playerId,
      boardPlayerType: boardPlayer.type,
      avatarId: boardPlayer.avatarId,
      nameInkConfirmed: String(boardPlayer.type) === "profile",
    });
    this.nextPlayerId += 1;
    return true;
  }

  private updateBoardPlayer(player: PlayerData, boardPlayer: BoardPlayer): boolean {
    const nextName = this.nameFromBoardPlayer(boardPlayer, player.id);
    if (
      player.displayName === nextName &&
      player.boardSessionId === boardPlayer.sessionId &&
      player.boardPlayerId === boardPlayer.playerId &&
      player.boardPlayerType === boardPlayer.type &&
      player.avatarId === boardPlayer.avatarId
    ) {
      return false;
    }

    player.displayName = nextName;
    player.boardSessionId = boardPlayer.sessionId;
    player.boardPlayerId = boardPlayer.playerId;
    player.boardPlayerType = boardPlayer.type;
    player.avatarId = boardPlayer.avatarId;
    if (this.isBoardProfile(player)) {
      player.nameInk = undefined;
      player.nameInkConfirmed = true;
    }
    return true;
  }

  private findBoardPlayer(boardPlayer: BoardPlayer): PlayerData | undefined {
    return this.players.find(
      (player) =>
        player.boardPlayerId === boardPlayer.playerId || player.boardSessionId === boardPlayer.sessionId,
    );
  }

  private showTitle(): void {
    this.phase = "title";
    this.stopCountdown();
    this.clearInputState();
    this.root.innerHTML = this.renderShell({
      className: "title",
      center: `
        <section class="title-stack">
          ${tableMarkSvg()}
          <p class="eyebrow">${escapeHtml(this.promptPack.packName)}</p>
          <h1>Table Laughs</h1>
          <p class="subtitle">A local tabletop prompt game for ${MIN_PLAYERS}-${MAX_PLAYERS} players.</p>
          <div class="title-actions">
            <button class="primary-command" id="start-button" type="button">Start</button>
          </div>
        </section>
      `,
      seats: this.renderEmptySeats(),
    });

    this.bindClick("start-button", () => this.beginJoin());
  }

  private beginJoin(): void {
    this.phase = "join";
    this.stopCountdown();
    this.clearInputState();
    this.players.length = 0;
    this.nextPlayerId = 1;
    this.importBoardSessionPlayers();
    this.showJoin();
  }

  private showJoin(): void {
    this.phase = "join";
    this.clearNameInputState();
    const needed = Math.max(0, MIN_PLAYERS - this.players.length);
    const statusText =
      needed === 0 ? "Ready when the table is" : `${needed} more player${needed === 1 ? "" : "s"} needed`;
    const boardActions = Board.isOnDevice ? "" : `<span class="status-pill">Browser preview</span>`;

    this.root.innerHTML = this.renderShell({
      className: "join",
      center: `
        <section class="center-card join-card">
          <p class="eyebrow">Join</p>
          <h2>Tap a seat to join</h2>
          <p class="round-status">${escapeHtml(statusText)}</p>
          <div class="join-count">${this.players.length}/${MAX_PLAYERS}</div>
          <div class="command-row">
            <button class="primary-command" id="begin-game" type="button" ${
              this.players.length < MIN_PLAYERS ? "disabled" : ""
            }>Begin Game</button>
            ${boardActions}
          </div>
        </section>
      `,
      seats: this.renderJoinSeats(),
    });

    this.bindClick("begin-game", () => this.startGame());

    for (let seatIndex = 0; seatIndex < MAX_PLAYERS; seatIndex += 1) {
      this.bindClick(`join-seat-${seatIndex}`, () => {
        this.joinSeat(seatIndex);
        this.showJoin();
      });
      this.bindClick(`board-seat-${seatIndex}`, () => void this.addBoardPlayer(seatIndex));
    }

    for (const player of this.players) {
      this.bindClick(`color-${player.id}`, () => {
        this.cyclePlayerColor(player);
        this.showJoin();
      });
      this.bindClick(`leave-${player.id}`, () => {
        this.leavePlayer(player.id);
        this.showJoin();
      });
    }

    this.bindGuestNameInputs();
    this.drawNamePreviews();
  }

  private async addBoardPlayer(seatIndex: number): Promise<void> {
    if (!Board.isOnDevice || !this.isOpenSeat(seatIndex) || this.players.length >= MAX_PLAYERS) {
      return;
    }

    try {
      const previousActiveProfileId = this.readActiveBoardProfile()?.playerId;
      const previousBoardPlayerKeys = this.boardSessionPlayerKeys();
      await Board.session.presentAddPlayer();

      if (this.importSelectedBoardPlayer(seatIndex, previousActiveProfileId, previousBoardPlayerKeys)) {
        this.showJoin();
        return;
      }

      this.startProfileSwitcherSync(seatIndex, previousActiveProfileId, previousBoardPlayerKeys);
    } catch (error) {
      console.warn("Add Board player failed.", error);
    }
  }

  private importSelectedBoardPlayer(
    preferredSeatIndex: number,
    previousActiveProfileId: string | undefined,
    previousBoardPlayerKeys: Set<string> | undefined,
  ): boolean {
    const newProfile = previousBoardPlayerKeys
      ? this.findNewBoardProfile(previousBoardPlayerKeys)
      : undefined;
    const activeProfile = this.readActiveBoardProfile();

    return (
      (newProfile !== undefined && this.importBoardPlayer(newProfile, preferredSeatIndex)) ||
      this.importBoardSessionPlayers(preferredSeatIndex) ||
      (activeProfile !== null &&
        activeProfile.playerId !== previousActiveProfileId &&
        this.importBoardPlayer(activeProfile, preferredSeatIndex))
    );
  }

  private findNewBoardProfile(previousBoardPlayerKeys: Set<string>): BoardPlayer | undefined {
    const boardPlayers = this.readBoardSessionPlayers();
    if (!boardPlayers) {
      return undefined;
    }

    return boardPlayers.find(
      (boardPlayer) =>
        String(boardPlayer.type) === "profile" &&
        !previousBoardPlayerKeys.has(this.boardPlayerKey(boardPlayer)),
    );
  }

  private readBoardSessionPlayers(): BoardPlayer[] | null {
    if (!Board.isOnDevice) {
      return null;
    }

    try {
      return Board.session.getPlayers();
    } catch (error) {
      console.warn("Could not read Board session players.", error);
      return null;
    }
  }

  private boardSessionPlayerKeys(): Set<string> {
    const boardPlayers = this.readBoardSessionPlayers();
    return new Set(boardPlayers?.map((boardPlayer) => this.boardPlayerKey(boardPlayer)) ?? []);
  }

  private boardPlayerKey(boardPlayer: BoardPlayer): string {
    return `${boardPlayer.playerId}:${boardPlayer.sessionId}:${String(boardPlayer.type)}`;
  }

  private readActiveBoardProfile(): BoardPlayer | null {
    if (!Board.isOnDevice) {
      return null;
    }

    try {
      return Board.session.getActiveProfile();
    } catch (error) {
      console.warn("Could not read active Board profile.", error);
      return null;
    }
  }

  private startProfileSwitcherSync(
    preferredSeatIndex?: number,
    previousActiveProfileId?: string,
    previousBoardPlayerKeys?: Set<string>,
  ): void {
    this.stopProfileSwitcherSync();

    const syncPlayers = (): void => {
      if (this.phase !== "join") {
        this.stopProfileSwitcherSync();
        return;
      }

      if (preferredSeatIndex === undefined) {
        if (this.importBoardSessionPlayers()) {
          this.showJoin();
        }
        return;
      }

      if (this.importSelectedBoardPlayer(preferredSeatIndex, previousActiveProfileId, previousBoardPlayerKeys)) {
        this.hideProfileSwitcher();
        this.stopProfileSwitcherSync();
        this.showJoin();
      }
    };

    syncPlayers();
    this.profileSwitcherSyncTimerId = window.setInterval(syncPlayers, 500);
  }

  private hideProfileSwitcher(): void {
    if (!Board.isOnDevice) {
      return;
    }

    try {
      Board.session.hideProfileSwitcher();
    } catch (error) {
      console.warn("Could not hide Board profile switcher.", error);
    }
  }

  private stopProfileSwitcherSync(): void {
    if (this.profileSwitcherSyncTimerId !== null) {
      window.clearInterval(this.profileSwitcherSyncTimerId);
      this.profileSwitcherSyncTimerId = null;
    }
  }

  private bindGuestNameInputs(): void {
    for (const player of this.players) {
      if (!this.needsGuestNameInk(player)) {
        continue;
      }

      const canvas = document.getElementById(`name-ink-${player.id}`) as HTMLCanvasElement | null;
      const clear = document.getElementById(`name-clear-${player.id}`) as HTMLButtonElement | null;
      const skip = document.getElementById(`name-skip-${player.id}`) as HTMLButtonElement | null;
      const done = document.getElementById(`name-done-${player.id}`) as HTMLButtonElement | null;
      if (!canvas || !clear || !skip || !done) {
        continue;
      }

      const pad = new InkPad(canvas, (answer) => {
        player.nameInk = answer;
        const hasInk = answerHasInk(answer);
        clear.disabled = !hasInk;
        done.disabled = !hasInk;
      });
      this.nameInkPads.set(player.id, pad);

      clear.addEventListener("click", () => pad.clear());
      skip.addEventListener("click", () => {
        player.nameInk = undefined;
        player.nameInkConfirmed = true;
        this.showJoin();
      });
      done.addEventListener("click", () => {
        const answer = pad.getAnswer();
        if (!answerHasInk(answer)) {
          return;
        }

        player.nameInk = answer;
        player.nameInkConfirmed = true;
        this.showJoin();
      });
    }
  }

  private startGame(): void {
    if (this.players.length < MIN_PLAYERS) {
      return;
    }

    this.players.forEach((player) => {
      player.score = 0;
    });
    this.currentRound = 1;
    this.resetPromptBags();
    this.runRound();
  }

  private runRound(): void {
    this.phase = "prompt";
    this.advancing = false;
    this.votedPlayerIds.clear();
    this.draftAnswers.clear();
    this.clearInputState();

    const prompt = this.currentRound === 3 ? this.drawFinalPrompt() : this.drawStandardPrompt();
    this.currentAnswers = this.players.map((player) => ({
      prompt,
      playerId: player.id,
      answer: blankAnswer(),
      submitted: false,
      votes: 0,
    }));

    this.showPromptEntry();
  }

  private showPromptEntry(): void {
    this.phase = "prompt";
    const roundLabel = this.currentRound === 3 ? "Final Round" : `Round ${this.currentRound}`;
    const timeLimit = PROMPT_SECONDS;
    const prompt = this.currentAnswers[0]?.prompt.text || "";

    this.root.innerHTML = this.renderShell({
      className: "prompt",
      center: `
        <section class="center-card prompt-card">
          <div class="round-heading">
            <span>${escapeHtml(roundLabel)}</span>
            ${this.renderTimer(timeLimit)}
          </div>
          <p class="eyebrow">${this.currentRound === 3 ? "Final prompt" : "Prompt"}</p>
          <h2>${escapeHtml(prompt)}</h2>
        </section>
      `,
      seats: this.renderPromptSeats(),
    });

    this.countdownEl = document.getElementById("countdown");
    this.bindPromptSeatInputs();
    this.drawNamePreviews();
    this.startCountdown(timeLimit, () => this.completePromptEntry());
  }

  private bindPromptSeatInputs(): void {
    for (const slot of this.currentAnswers) {
      if (slot.submitted) {
        continue;
      }

      const canvas = document.getElementById(`ink-${slot.playerId}`) as HTMLCanvasElement | null;
      const submit = document.getElementById(`submit-${slot.playerId}`) as HTMLButtonElement | null;
      const clear = document.getElementById(`clear-${slot.playerId}`) as HTMLButtonElement | null;
      const random = document.getElementById(`random-${slot.playerId}`) as HTMLButtonElement | null;
      if (!canvas || !submit || !clear || !random) {
        continue;
      }

      const pad = new InkPad(canvas, (answer) => {
        this.draftAnswers.set(slot.playerId, answer);
        const hasInk = answerHasInk(answer);
        submit.disabled = !hasInk;
        clear.disabled = !hasInk;
      });
      this.inkPads.set(slot.playerId, pad);
      submit.disabled = true;
      clear.disabled = true;

      submit.addEventListener("click", () => {
        this.submitAnswer(slot.playerId, pad.getAnswer());
      });
      clear.addEventListener("click", () => pad.clear());
      random.addEventListener("click", () => {
        this.submitAnswer(slot.playerId, {
          text: this.randomFallbackAnswer(),
          strokes: [],
        });
      });
    }
  }

  private submitAnswer(playerId: number, answer: HandwritingAnswer): void {
    const slot = this.currentAnswers.find((candidate) => candidate.playerId === playerId);
    if (!slot || slot.submitted) {
      return;
    }

    slot.answer = this.cleanAnswer(answer);
    slot.submitted = true;
    this.inkPads.get(playerId)?.dispose();
    this.inkPads.delete(playerId);

    const panel = document.getElementById(`prompt-panel-${playerId}`);
    if (panel) {
      panel.innerHTML = this.renderSubmittedSeat(slot);
      this.drawPreviews();
      this.drawNamePreviews();
    }

    if (this.currentAnswers.every((candidate) => candidate.submitted)) {
      window.setTimeout(() => this.completePromptEntry(), 250);
    }
  }

  private completePromptEntry(): void {
    if (this.advancing || this.phase !== "prompt") {
      return;
    }

    this.advancing = true;
    this.stopCountdown();
    for (const slot of this.currentAnswers) {
      if (!slot.submitted) {
        slot.answer = { text: this.randomFallbackAnswer(), strokes: [] };
        slot.submitted = true;
      }
    }

    this.currentAnswers.forEach((slot) => {
      slot.votes = 0;
    });
    this.votedPlayerIds.clear();
    window.setTimeout(() => this.showVoting(), 300);
  }

  private showVoting(): void {
    this.phase = "vote";
    this.advancing = false;
    const isFinal = this.currentRound === 3;
    const timeLimit = isFinal ? FINAL_VOTE_SECONDS : ROUND_VOTE_SECONDS;
    const answerCards = this.currentAnswers
      .map((slot, index) => this.renderAnswerCard(slot, index, "vote-card"))
      .join("");

    this.root.innerHTML = this.renderShell({
      className: "vote",
      center: `
        <section class="vote-stage">
          <div class="round-heading">
            <span>${isFinal ? "Final Vote" : `Round ${this.currentRound} Vote`}</span>
            ${this.renderTimer(timeLimit)}
          </div>
          <div class="voting-prompt">${escapeHtml(this.currentAnswers[0]?.prompt.text || "")}</div>
          <div class="answer-grid answer-count-${this.currentAnswers.length}">
            ${answerCards}
          </div>
        </section>
      `,
      seats: this.renderVoteSeats(),
    });

    this.countdownEl = document.getElementById("countdown");
    this.bindVoteButtons();
    this.drawPreviews();
    this.drawNamePreviews();
    this.startCountdown(timeLimit, () => this.completeVoting());
  }

  private bindVoteButtons(): void {
    this.root.querySelectorAll<HTMLButtonElement>("[data-vote-index]").forEach((button) => {
      button.addEventListener("click", () => {
        const playerId = Number(button.dataset.playerId);
        const answerIndex = Number(button.dataset.voteIndex);
        if (this.castVote(playerId, answerIndex)) {
          const panel = button.closest<HTMLElement>(".seat-panel");
          panel?.querySelectorAll<HTMLButtonElement>("[data-vote-index]").forEach((choice) => {
            choice.disabled = true;
          });
          const status = panel?.querySelector<HTMLElement>(".vote-status");
          if (status) {
            status.textContent = "Voted";
          }
        }
      });
    });
  }

  private castVote(playerId: number, answerIndex: number): boolean {
    if (this.votedPlayerIds.has(playerId)) {
      return false;
    }

    const player = this.players.find((candidate) => candidate.id === playerId);
    const selected = this.currentAnswers[answerIndex];
    if (!player || !selected || selected.playerId === player.id) {
      return false;
    }

    selected.votes += 1;
    this.votedPlayerIds.add(playerId);

    if (this.votedPlayerIds.size >= this.players.length) {
      window.setTimeout(() => this.completeVoting(), 250);
    }

    return true;
  }

  private completeVoting(): void {
    if (this.advancing || this.phase !== "vote") {
      return;
    }

    this.advancing = true;
    this.stopCountdown();
    this.showResults();
  }

  private showResults(): void {
    this.phase = "results";
    const isFinal = this.currentRound === 3;
    const sorted = [...this.currentAnswers].sort((a, b) => b.votes - a.votes);
    const results = sorted.map((slot, index) => this.renderResultRow(slot, index)).join("");

    this.root.innerHTML = this.renderShell({
      className: "results",
      center: `
        <section class="center-card results-card">
          <p class="eyebrow">${isFinal ? "Final votes" : `Round ${this.currentRound} votes`}</p>
          <h2>${escapeHtml(this.currentAnswers[0]?.prompt.text || "")}</h2>
          <div class="result-list">${results}</div>
          <button class="primary-command" id="continue-from-results" type="button">${
            isFinal ? "Show Champion" : "Scores"
          }</button>
        </section>
      `,
      seats: this.renderScoreSeats(),
    });

    this.drawPreviews();
    this.drawNamePreviews();
    this.bindClick("continue-from-results", () => {
      const summary = this.applyRoundScores();
      if (isFinal) {
        this.showWinner();
      } else {
        this.showScoreboard(summary);
      }
    });
  }

  private showScoreboard(summary: ScoreSummary): void {
    this.phase = "scoreboard";
    const leaders = this.leaderboard();
    const rows = leaders
      .map(
        (player, index) => `
          <li class="leader-row" style="--player-color:${player.color}">
            <span class="leader-rank">${index + 1}.</span>
            <div class="leader-name">${this.renderPlayerName(player, "leader")}</div>
            <strong>${player.score}</strong>
          </li>
        `,
      )
      .join("");
    const summaryText =
      summary.lines.length === 0
        ? "No points this time. The table remains mysterious."
        : summary.lines.slice(0, 3).join(" | ");

    this.root.innerHTML = this.renderShell({
      className: "scoreboard",
      center: `
        <section class="center-card scoreboard-card">
          <p class="eyebrow">Scores after round ${this.currentRound}</p>
          <ol class="leaderboard">${rows}</ol>
          <p class="score-summary">${escapeHtml(summaryText)}</p>
          <button class="primary-command" id="next-round" type="button">Next Round</button>
        </section>
      `,
      seats: this.renderScoreSeats(),
    });

    this.bindClick("next-round", () => {
      this.currentRound += 1;
      this.runRound();
    });
    this.drawNamePreviews();
  }

  private showWinner(): void {
    this.phase = "winner";
    const leaders = this.leaderboard();
    const winner = leaders[0];
    const rows = leaders
      .map(
        (player, index) => `
          <li class="leader-row" style="--player-color:${player.color}">
            <span class="leader-rank">${index + 1}.</span>
            <div class="leader-name">${this.renderPlayerName(player, "leader")}</div>
            <strong>${player.score}</strong>
          </li>
        `,
      )
      .join("");

    this.root.innerHTML = this.renderShell({
      className: "winner",
      center: `
        <section class="center-card winner-card" style="--winner-color:${winner.color}">
          ${confettiMarkup()}
          <p class="eyebrow">Table Laughs Champion</p>
          <div class="winner-name">${this.renderPlayerName(winner, "winner")}</div>
          <ol class="leaderboard">${rows}</ol>
          <button class="primary-command" id="play-again" type="button">Play Again</button>
        </section>
      `,
      seats: this.renderScoreSeats(),
    });

    this.bindClick("play-again", () => this.beginJoin());
    this.drawNamePreviews();
    void this.saveSnapshot();
  }

  private renderShell(options: { className: string; center: string; seats: string }): string {
    return `
      <main class="game-shell phase-${options.className}">
        <div class="device-bar">
          <span class="brand">Table Laughs</span>
          <span>${Board.isOnDevice ? "Board device" : "Browser preview"}</span>
          <span>SDK ${escapeHtml(Board.sdkVersion)}</span>
          <span>Bridge ${escapeHtml(String(Board.bridgeVersion ?? "n/a"))}</span>
          <span id="contact-readout">${escapeHtml(this.contactSummary)}</span>
        </div>
        <section class="table-surface" aria-live="polite">
          <div class="felt-pattern" aria-hidden="true"></div>
          <div class="center-zone">${options.center}</div>
          <div class="seat-layer">${options.seats}</div>
        </section>
      </main>
    `;
  }

  private renderEmptySeats(): string {
    return Array.from({ length: MAX_PLAYERS }, (_, seatIndex) =>
      this.renderSeatPanel(seatIndex, "empty", `<span class="seat-number">Seat ${seatIndex + 1}</span>`),
    ).join("");
  }

  private renderJoinSeats(): string {
    return Array.from({ length: MAX_PLAYERS }, (_, seatIndex) => {
      const player = this.playerAtSeat(seatIndex);
      if (!player) {
        return this.renderSeatPanel(
          seatIndex,
          "join-open",
          this.renderOpenSeatContents(seatIndex),
        );
      }

      return this.renderSeatPanel(
        seatIndex,
        "join-player",
        this.renderJoinPlayerContents(player),
        player,
      );
    }).join("");
  }

  private renderOpenSeatContents(seatIndex: number): string {
    const boardDisabled = Board.isOnDevice ? "" : "disabled";
    return `
      <div class="seat-join-options">
        <span class="seat-label">Seat ${seatIndex + 1}</span>
        <button class="seat-join-button seat-board-button" id="board-seat-${seatIndex}" type="button" ${boardDisabled}>
          <span>Add</span>
          <strong>Board Player</strong>
        </button>
        <button class="seat-join-button" id="join-seat-${seatIndex}" type="button">
          <span>Add</span>
          <strong>Guest</strong>
        </button>
      </div>
    `;
  }

  private renderJoinPlayerContents(player: PlayerData): string {
    if (this.needsGuestNameInk(player)) {
      return `
        <div class="name-writer">
          <span class="seat-label">Seat ${player.seatIndex + 1} - Guest</span>
          <p>Write your name</p>
          <canvas class="name-ink-pad" id="name-ink-${player.id}" width="360" height="96"></canvas>
          <div class="seat-actions three">
            <button id="name-clear-${player.id}" type="button" disabled>Clear</button>
            <button id="name-skip-${player.id}" type="button">Skip</button>
            <button id="name-done-${player.id}" type="button" disabled>Done</button>
          </div>
        </div>
      `;
    }

    return `
      <span class="seat-label">Seat ${player.seatIndex + 1} - ${this.playerKindLabel(player)}</span>
      <div class="join-name">${this.renderPlayerName(player, "standard")}</div>
      <div class="seat-actions">
        <button id="color-${player.id}" type="button">Color</button>
        <button id="leave-${player.id}" type="button">Leave</button>
      </div>
    `;
  }

  private renderPromptSeats(): string {
    return this.players
      .map((player) => {
        const slot = this.currentAnswers.find((answer) => answer.playerId === player.id);
        if (!slot) {
          return "";
        }

        const body = slot.submitted
          ? this.renderSubmittedSeat(slot)
          : `
            <div class="seat-player-name">${this.renderPlayerName(player, "compact")}</div>
            <p class="seat-prompt">${escapeHtml(slot.prompt.text)}</p>
            <canvas class="ink-pad" id="ink-${player.id}" width="420" height="128"></canvas>
            <div class="seat-actions three">
              <button id="clear-${player.id}" type="button" disabled>Clear</button>
              <button id="random-${player.id}" type="button">Random</button>
              <button id="submit-${player.id}" type="button" disabled>Submit</button>
            </div>
          `;
        return this.renderSeatPanel(
          player.seatIndex,
          "prompt-seat",
          `<div id="prompt-panel-${player.id}" class="prompt-panel">${body}</div>`,
          player,
        );
      })
      .join("");
  }

  private renderSubmittedSeat(slot: AnswerSlot): string {
    const player = this.getPlayer(slot.playerId);
    return `
      <div class="seat-player-name">${this.renderPlayerName(player, "compact")}</div>
      <span class="submitted-badge">Submitted</span>
      <div class="seat-answer-hidden">Answer hidden</div>
    `;
  }

  private renderVoteSeats(): string {
    return this.players
      .map((player) => {
        const choices = this.currentAnswers
          .map((slot, index) => {
            const isOwn = slot.playerId === player.id;
            return `
              <button
                type="button"
                data-player-id="${player.id}"
                data-vote-index="${index}"
                ${isOwn ? "disabled" : ""}
              >${index + 1}</button>
            `;
          })
          .join("");
        return this.renderSeatPanel(
          player.seatIndex,
          "vote-seat",
          `
            <div class="seat-player-name">${this.renderPlayerName(player, "compact")}</div>
            <span class="vote-status">Vote</span>
            <div class="vote-buttons">${choices}</div>
          `,
          player,
        );
      })
      .join("");
  }

  private renderScoreSeats(): string {
    return this.players
      .map((player) =>
        this.renderSeatPanel(
          player.seatIndex,
          "score-seat",
          `
            <div class="seat-player-name">${this.renderPlayerName(player, "compact")}</div>
            <strong>${player.score}</strong>
          `,
          player,
        ),
      )
      .join("");
  }

  private renderSeatPanel(
    seatIndex: number,
    mode: string,
    contents: string,
    player?: PlayerData,
  ): string {
    const colorStyle = player ? `style="--player-color:${player.color}"` : "";
    return `
      <article class="seat-panel seat-${seatIndex} ${mode}" ${colorStyle}>
        ${contents}
      </article>
    `;
  }

  private renderAnswerCard(slot: AnswerSlot, index: number, extraClass: string): string {
    const player = this.getPlayer(slot.playerId);
    return `
      <article class="answer-card ${extraClass}" style="--player-color:${player?.color || "#f5ba3d"}">
        <span class="answer-number">${index + 1}</span>
        ${this.renderAnswerPreview(slot, "answer-preview")}
      </article>
    `;
  }

  private renderResultRow(slot: AnswerSlot, index: number): string {
    const player = this.getPlayer(slot.playerId);
    return `
      <article class="result-row" style="--player-color:${player?.color || "#f5ba3d"}">
        <span class="result-rank">${index + 1}</span>
        <div class="result-name">${this.renderPlayerName(player, "compact")}</div>
        ${this.renderAnswerPreview(slot, "result-preview")}
        <span>${slot.votes} vote${slot.votes === 1 ? "" : "s"}</span>
      </article>
    `;
  }

  private renderAnswerPreview(slot: AnswerSlot, className: string): string {
    const previewId = `${slot.playerId}-${this.currentRound}-${slot.prompt.id}-${slot.votes}`;
    if (answerHasInk(slot.answer)) {
      return `<canvas class="${className} ink-preview" width="500" height="180" data-preview-id="${escapeAttribute(
        previewId,
      )}" data-player-id="${slot.playerId}"></canvas>`;
    }

    return `<div class="${className} text-preview">${escapeHtml(slot.answer.text || "Handwritten answer")}</div>`;
  }

  private renderPlayerName(player: PlayerData | undefined, size: "compact" | "standard" | "leader" | "winner"): string {
    if (!player) {
      return `<span class="player-name player-name-${size} player-name-text">Player</span>`;
    }

    if (player.nameInk && answerHasInk(player.nameInk)) {
      return `<canvas class="player-name player-name-${size} player-name-ink" width="360" height="96" data-name-player-id="${
        player.id
      }" aria-label="${escapeAttribute(this.playerLabelText(player))}"></canvas>`;
    }

    return `<span class="player-name player-name-${size} player-name-text">${escapeHtml(
      this.playerLabelText(player),
    )}</span>`;
  }

  private renderTimer(seconds: number): string {
    return `<span class="timer"><span id="countdown">${Math.ceil(seconds)}</span></span>`;
  }

  private drawPreviews(): void {
    this.root.querySelectorAll<HTMLCanvasElement>("canvas[data-preview-id]").forEach((canvas) => {
      const playerId = Number(canvas.dataset.playerId);
      const slot = this.currentAnswers.find((answer) => answer.playerId === playerId);
      if (slot) {
        drawInkPreview(canvas, slot.answer);
      }
    });
  }

  private drawNamePreviews(): void {
    this.root.querySelectorAll<HTMLCanvasElement>("canvas[data-name-player-id]").forEach((canvas) => {
      const playerId = Number(canvas.dataset.namePlayerId);
      const player = this.getPlayer(playerId);
      if (player?.nameInk) {
        drawInkPreview(canvas, player.nameInk);
      }
    });
  }

  private startCountdown(seconds: number, onExpired: () => void): void {
    this.stopCountdown();
    this.timerEndsAt = Date.now() + seconds * 1000;
    this.updateCountdown();
    this.timerId = window.setInterval(() => {
      this.updateCountdown();
      if (Date.now() >= this.timerEndsAt) {
        this.stopCountdown();
        onExpired();
      }
    }, 250);
  }

  private updateCountdown(): void {
    if (!this.countdownEl) {
      return;
    }

    const remaining = Math.max(0, Math.ceil((this.timerEndsAt - Date.now()) / 1000));
    this.countdownEl.textContent = String(remaining);
  }

  private stopCountdown(): void {
    if (this.timerId !== null) {
      window.clearInterval(this.timerId);
      this.timerId = null;
    }
  }

  private joinSeat(seatIndex: number): void {
    if (
      seatIndex < 0 ||
      seatIndex >= MAX_PLAYERS ||
      this.players.length >= MAX_PLAYERS ||
      this.playerAtSeat(seatIndex)
    ) {
      return;
    }

    const id = this.nextPlayerId;
    const boardSessionId = Board.isOnDevice ? this.nextGuestSessionId() : id;
    if (Board.isOnDevice) {
      try {
        Board.session.addGuest(boardSessionId);
      } catch (error) {
        console.warn("Could not add Board guest.", error);
      }
    }

    this.players.push({
      id,
      seatIndex,
      displayName: `Player ${id}`,
      color: PLAYER_COLORS[(id - 1) % PLAYER_COLORS.length],
      score: 0,
      boardSessionId,
      boardPlayerType: "guest",
      nameInkConfirmed: false,
    });
    this.nextPlayerId += 1;
  }

  private leavePlayer(playerId: number): void {
    const player = this.getPlayer(playerId);
    if (player?.boardSessionId !== undefined && Board.isOnDevice) {
      try {
        Board.session.removePlayer(player.boardSessionId);
      } catch (error) {
        console.warn("Could not remove Board player.", error);
      }
    }

    const index = this.players.findIndex((candidate) => candidate.id === playerId);
    if (index >= 0) {
      this.nameInkPads.get(playerId)?.dispose();
      this.nameInkPads.delete(playerId);
      this.players.splice(index, 1);
    }
  }

  private cyclePlayerColor(player: PlayerData): void {
    const currentIndex = PLAYER_COLORS.indexOf(player.color);
    player.color = PLAYER_COLORS[(currentIndex + 1) % PLAYER_COLORS.length];
  }

  private needsGuestNameInk(player: PlayerData): boolean {
    return !this.isBoardProfile(player) && player.nameInkConfirmed !== true;
  }

  private isBoardProfile(player: PlayerData): boolean {
    return String(player.boardPlayerType) === "profile";
  }

  private playerKindLabel(player: PlayerData): string {
    return this.isBoardProfile(player) ? "Profile" : "Guest";
  }

  private playerLabelText(player: PlayerData): string {
    return cleanName(player.displayName, player.id);
  }

  private nameFromBoardPlayer(boardPlayer: BoardPlayer, fallbackId: number): string {
    if (String(boardPlayer.type) !== "profile") {
      return `Player ${fallbackId}`;
    }

    return cleanName(boardPlayer.name, fallbackId);
  }

  private playerAtSeat(seatIndex: number): PlayerData | undefined {
    return this.players.find((player) => player.seatIndex === seatIndex);
  }

  private getPlayer(playerId: number): PlayerData | undefined {
    return this.players.find((player) => player.id === playerId);
  }

  private firstOpenSeat(preferredSeatIndex?: number): number {
    if (preferredSeatIndex !== undefined && this.isOpenSeat(preferredSeatIndex)) {
      return preferredSeatIndex;
    }

    for (let seatIndex = 0; seatIndex < MAX_PLAYERS; seatIndex += 1) {
      if (this.isOpenSeat(seatIndex)) {
        return seatIndex;
      }
    }

    return -1;
  }

  private isOpenSeat(seatIndex: number | undefined): boolean {
    return (
      seatIndex !== undefined &&
      seatIndex >= 0 &&
      seatIndex < MAX_PLAYERS &&
      !this.playerAtSeat(seatIndex)
    );
  }

  private nextGuestSessionId(): number {
    let sessionId = this.nextPlayerId;
    const boardSessionIds = new Set(
      this.readBoardSessionPlayers()?.map((player) => player.sessionId) ?? [],
    );
    while (
      this.players.some((player) => player.boardSessionId === sessionId) ||
      boardSessionIds.has(sessionId)
    ) {
      sessionId += 1;
    }

    return sessionId;
  }

  private resetPromptBags(): void {
    this.standardBag.length = 0;
    this.standardBag.push(...shuffle([...this.promptPack.prompts]));
    this.finalBag.length = 0;
    this.finalBag.push(...shuffle([...this.promptPack.finalPrompts]));
  }

  private drawStandardPrompt(): PromptEntry {
    if (this.standardBag.length === 0) {
      this.standardBag.push(...shuffle([...this.promptPack.prompts]));
    }

    return this.standardBag.shift() || fallbackPromptPack.prompts[0];
  }

  private drawFinalPrompt(): PromptEntry {
    if (this.finalBag.length === 0) {
      this.finalBag.push(...shuffle([...this.promptPack.finalPrompts]));
    }

    return this.finalBag.shift() || fallbackPromptPack.finalPrompts[0];
  }

  private randomFallbackAnswer(): string {
    const answers = this.promptPack.randomAnswers.length
      ? this.promptPack.randomAnswers
      : fallbackPromptPack.randomAnswers;
    return answers[Math.floor(Math.random() * answers.length)] || "A very confident sandwich";
  }

  private cleanAnswer(answer: HandwritingAnswer): HandwritingAnswer {
    const clone = cloneAnswer(answer);
    if (answerHasInk(clone)) {
      clone.text = cleanText(clone.text, "Handwritten answer");
      return clone;
    }

    clone.text = cleanText(clone.text, "A tiny parade of waffles");
    return clone;
  }

  private applyRoundScores(): ScoreSummary {
    const summary: ScoreSummary = { lines: [] };
    const pointsPerVote = this.currentRound === 1 ? 100 : this.currentRound === 2 ? 200 : 300;
    const possibleVotes = Math.max(0, this.players.length - 1);

    for (const answer of this.currentAnswers) {
      const player = this.getPlayer(answer.playerId);
      if (!player) {
        continue;
      }

      let points = answer.votes * pointsPerVote;
      const earnedSweep = possibleVotes > 0 && answer.votes === possibleVotes;
      if (earnedSweep) {
        points += 250 * Math.max(1, pointsPerVote / 100);
      }

      player.score += points;
      if (points > 0) {
        const playerName = this.playerLabelText(player);
        summary.lines.push(
          earnedSweep
            ? `${playerName}: +${points} including a table sweep bonus`
            : `${playerName}: +${points}`,
        );
      }
    }

    return summary;
  }

  private leaderboard(): PlayerData[] {
    return [...this.players].sort((a, b) => b.score - a.score || a.displayName.localeCompare(b.displayName));
  }

  private async saveSnapshot(): Promise<void> {
    const payload = new TextEncoder().encode(
      JSON.stringify({
        phase: this.phase,
        currentRound: this.currentRound,
        players: this.players,
        savedAt: Date.now(),
      }),
    );

    if (!Board.isOnDevice) {
      window.localStorage.setItem("table-laughs-save", new TextDecoder().decode(payload));
      return;
    }

    try {
      const description = `Table Laughs - ${new Date().toLocaleString()}`;
      const playedTime = Date.now() - this.startedAt;
      if (this.saveId) {
        await Board.save.update(this.saveId, description, payload, playedTime, GAME_VERSION);
      } else {
        const metadata = await Board.save.create(description, payload, playedTime, GAME_VERSION);
        this.saveId = metadata.id;
      }
    } catch (error) {
      console.warn("Save failed.", error);
    }
  }

  private clearInputState(): void {
    this.inkPads.forEach((pad) => pad.dispose());
    this.inkPads.clear();
    this.clearNameInputState();
  }

  private clearNameInputState(): void {
    this.nameInkPads.forEach((pad) => pad.dispose());
    this.nameInkPads.clear();
  }

  private bindClick(id: string, handler: () => void): void {
    const element = document.getElementById(id);
    element?.addEventListener("click", handler);
  }
}

class InkPad {
  private readonly canvas: HTMLCanvasElement;
  private readonly context: CanvasRenderingContext2D;
  private readonly onChange: (answer: HandwritingAnswer) => void;
  private readonly answer = blankAnswer();
  private activePointerId: number | null = null;
  private activeStroke: HandwritingStroke | null = null;
  private resizeObserver: ResizeObserver | null = null;

  constructor(canvas: HTMLCanvasElement, onChange: (answer: HandwritingAnswer) => void) {
    const context = canvas.getContext("2d");
    if (!context) {
      throw new Error("Canvas 2D context is unavailable.");
    }

    this.canvas = canvas;
    this.context = context;
    this.onChange = onChange;
    this.resize();
    this.resizeObserver = new ResizeObserver(() => this.resize());
    this.resizeObserver.observe(this.canvas);

    this.canvas.addEventListener("pointerdown", this.handlePointerDown);
    this.canvas.addEventListener("pointermove", this.handlePointerMove);
    this.canvas.addEventListener("pointerup", this.handlePointerUp);
    this.canvas.addEventListener("pointercancel", this.handlePointerUp);
  }

  getAnswer(): HandwritingAnswer {
    return cloneAnswer(this.answer);
  }

  clear(): void {
    this.answer.text = "";
    this.answer.strokes.length = 0;
    this.activeStroke = null;
    this.activePointerId = null;
    this.redraw();
    this.onChange(this.getAnswer());
  }

  dispose(): void {
    this.resizeObserver?.disconnect();
    this.canvas.removeEventListener("pointerdown", this.handlePointerDown);
    this.canvas.removeEventListener("pointermove", this.handlePointerMove);
    this.canvas.removeEventListener("pointerup", this.handlePointerUp);
    this.canvas.removeEventListener("pointercancel", this.handlePointerUp);
  }

  private readonly handlePointerDown = (event: PointerEvent): void => {
    if (this.activePointerId !== null) {
      return;
    }

    const point = this.pointFromEvent(event);
    this.activePointerId = event.pointerId;
    this.activeStroke = { points: [point] };
    this.answer.strokes.push(this.activeStroke);
    this.canvas.setPointerCapture(event.pointerId);
    this.redraw();
    this.onChange(this.getAnswer());
  };

  private readonly handlePointerMove = (event: PointerEvent): void => {
    if (this.activePointerId !== event.pointerId || !this.activeStroke) {
      return;
    }

    const point = this.pointFromEvent(event);
    const previous = this.activeStroke.points[this.activeStroke.points.length - 1];
    const dx = point.x - previous.x;
    const dy = point.y - previous.y;
    if (dx * dx + dy * dy < 0.00025) {
      return;
    }

    this.activeStroke.points.push(point);
    this.redraw();
    this.onChange(this.getAnswer());
  };

  private readonly handlePointerUp = (event: PointerEvent): void => {
    if (this.activePointerId !== event.pointerId) {
      return;
    }

    this.activePointerId = null;
    this.activeStroke = null;
    if (this.canvas.hasPointerCapture(event.pointerId)) {
      this.canvas.releasePointerCapture(event.pointerId);
    }
  };

  private pointFromEvent(event: PointerEvent): InkPoint {
    // Keep ink in the canvas-local frame; the seat panel may be CSS-rotated.
    const width = this.canvas.clientWidth || this.canvas.width;
    const height = this.canvas.clientHeight || this.canvas.height;
    const x = width === 0 ? 0 : event.offsetX / width;
    const y = height === 0 ? 0 : event.offsetY / height;
    return {
      x: clamp(x, 0, 1),
      y: clamp(y, 0, 1),
    };
  }

  private resize(): void {
    const { width, height } = canvasPixelSize(this.canvas);
    if (this.canvas.width !== width || this.canvas.height !== height) {
      this.canvas.width = width;
      this.canvas.height = height;
    }
    this.redraw();
  }

  private redraw(): void {
    drawInkPreview(this.canvas, this.answer);
  }
}

function drawInkPreview(canvas: HTMLCanvasElement, answer: HandwritingAnswer): void {
  const context = canvas.getContext("2d");
  if (!context) {
    return;
  }

  const { width, height } = canvasPixelSize(canvas);
  if (canvas.width !== width || canvas.height !== height) {
    canvas.width = width;
    canvas.height = height;
  }

  context.setTransform(1, 0, 0, 1, 0, 0);
  context.clearRect(0, 0, width, height);
  context.fillStyle = "#fbf4dc";
  context.fillRect(0, 0, width, height);
  context.strokeStyle = "rgba(44, 57, 55, 0.16)";
  context.lineWidth = Math.max(1, height / 18);
  for (let y = height * 0.32; y < height; y += height * 0.26) {
    context.beginPath();
    context.moveTo(width * 0.05, y);
    context.lineTo(width * 0.95, y);
    context.stroke();
  }

  if (!answerHasInk(answer)) {
    drawWrappedText(context, answer.text, width, height);
    return;
  }

  context.strokeStyle = "#202726";
  context.lineCap = "round";
  context.lineJoin = "round";
  context.lineWidth = Math.max(5, height * 0.07);
  for (const stroke of answer.strokes) {
    if (stroke.points.length === 0) {
      continue;
    }

    context.beginPath();
    context.moveTo(stroke.points[0].x * width, stroke.points[0].y * height);
    for (let index = 1; index < stroke.points.length; index += 1) {
      context.lineTo(stroke.points[index].x * width, stroke.points[index].y * height);
    }
    context.stroke();

    if (stroke.points.length === 1) {
      context.beginPath();
      context.arc(stroke.points[0].x * width, stroke.points[0].y * height, context.lineWidth / 2, 0, Math.PI * 2);
      context.fillStyle = "#202726";
      context.fill();
    }
  }
}

function drawWrappedText(
  context: CanvasRenderingContext2D,
  text: string,
  width: number,
  height: number,
): void {
  const content = text || "Handwritten answer";
  const fontSize = Math.max(18, Math.min(34, height * 0.28));
  const lineHeight = fontSize * 1.15;
  const maxWidth = width * 0.84;
  const words = content.split(/\s+/);
  const lines: string[] = [];
  let line = "";

  context.font = `700 ${fontSize}px system-ui, -apple-system, Segoe UI, sans-serif`;
  for (const word of words) {
    const test = line ? `${line} ${word}` : word;
    if (context.measureText(test).width > maxWidth && line) {
      lines.push(line);
      line = word;
    } else {
      line = test;
    }
  }
  if (line) {
    lines.push(line);
  }

  context.fillStyle = "#202726";
  context.textAlign = "center";
  context.textBaseline = "middle";
  const startY = height / 2 - ((lines.length - 1) * lineHeight) / 2;
  lines.slice(0, 3).forEach((row, index) => {
    context.fillText(row, width / 2, startY + index * lineHeight, maxWidth);
  });
}

function tableMarkSvg(): string {
  return `
    <svg class="table-mark" viewBox="0 0 220 160" aria-hidden="true">
      <rect x="22" y="18" width="176" height="124" rx="26" fill="#2c5e4e" />
      <rect x="39" y="35" width="142" height="90" rx="20" fill="#347463" />
      <path d="M55 64h52M55 84h74M55 104h42" stroke="#f8d96e" stroke-width="10" stroke-linecap="round" />
      <rect x="134" y="54" width="34" height="50" rx="8" fill="#fbf4dc" />
      <path d="M140 70h22M140 86h16" stroke="#202726" stroke-width="5" stroke-linecap="round" />
      <circle cx="42" cy="27" r="13" fill="#f35f6f" />
      <circle cx="181" cy="130" r="13" fill="#31a9ee" />
      <circle cx="32" cy="130" r="13" fill="#f5ba3d" />
      <circle cx="188" cy="31" r="13" fill="#37c978" />
    </svg>
  `;
}

function confettiMarkup(): string {
  return `
    <div class="confetti" aria-hidden="true">
      ${Array.from(
        { length: 18 },
        (_, index) => `<i style="--i:${index}; --delay:${(index % 6) * 0.12}s"></i>`,
      ).join("")}
    </div>
  `;
}

function canvasPixelSize(canvas: HTMLCanvasElement): { width: number; height: number } {
  const width = canvas.clientWidth || canvas.width;
  const height = canvas.clientHeight || canvas.height;
  return {
    width: Math.max(1, Math.round(width * window.devicePixelRatio)),
    height: Math.max(1, Math.round(height * window.devicePixelRatio)),
  };
}

function answerHasInk(answer: HandwritingAnswer): boolean {
  return answer.strokes.some((stroke) => stroke.points.length > 0);
}

function cloneAnswer(answer: HandwritingAnswer): HandwritingAnswer {
  return {
    text: answer.text || "",
    strokes: answer.strokes.map((stroke) => ({
      points: stroke.points.map((point) => ({ x: point.x, y: point.y })),
    })),
  };
}

function cleanText(value: string, fallback: string): string {
  const cleaned = value.trim() || fallback;
  return cleaned.length > 80 ? cleaned.slice(0, 80) : cleaned;
}

function cleanName(value: string, playerId: number): string {
  const cleaned = value.trim();
  return cleaned.length > 0 ? cleaned.slice(0, 16) : `Player ${playerId}`;
}

function shuffle<T>(values: T[]): T[] {
  for (let index = values.length - 1; index > 0; index -= 1) {
    const swapIndex = Math.floor(Math.random() * (index + 1));
    [values[index], values[swapIndex]] = [values[swapIndex], values[index]];
  }
  return values;
}

function clamp(value: number, min: number, max: number): number {
  return Math.min(max, Math.max(min, value));
}

function escapeHtml(value: string): string {
  return value.replace(/[&<>"']/g, (character) => {
    const replacements: Record<string, string> = {
      "&": "&amp;",
      "<": "&lt;",
      ">": "&gt;",
      '"': "&quot;",
      "'": "&#39;",
    };
    return replacements[character] || character;
  });
}

function escapeAttribute(value: string): string {
  return escapeHtml(value).replace(/`/g, "&#96;");
}
