import * as fs from "fs";

import {
    BaseErrorListener, CharStream, CommonTokenStream, ParserRuleContext, RecognitionException,
    Recognizer, Token, ATNSimulator,
} from "antlr4ng";
import { describe, expect, it } from "vitest";

import { WhiteboxLexer } from "./generated/WhiteboxLexer";
import { CodeCompletionCore } from "../src/CodeCompletionCore";

import { TSqlLexer } from "./generated/TSqlLexer.js";
import { TSqlParser } from "./generated/TSqlParser.js";

export class TestErrorListener extends BaseErrorListener {
    public errorCount = 0;

    public override syntaxError<S extends Token, T extends ATNSimulator>(_recognizer: Recognizer<T>,
        _offendingSymbol: S | null, _line: number, _column: number, _msg: string,
        _e: RecognitionException | null): void {
        ++this.errorCount;
    }
}

describe("Code Completion Tests", () => {
    describe("Whitebox grammar tests:", () => {

        // Whitespace tokens are skipped
        it("Caret at transition to rule with non-exhaustive follow set (optional tokens)", () => {

            const input = "SELECT ";
            const inputStream = CharStream.fromString(input);
            const lexer = new TSqlLexer(inputStream);
            const tokens = new CommonTokenStream(lexer);
            const parser = new TSqlParser(tokens);
            parser.buildParseTrees = true;
            const tree = parser.tsql_file();

            const core = new CodeCompletionCore(parser);
            const candidates = core.collectCandidates(tokens.size - 1);

            console.log("Candidates:", candidates);

            expect(candidates.tokens.size).toEqual(5);
        });

    });
});
