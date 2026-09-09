/*
 * This file is released under the MIT license.
 * Copyright (c) 2026 Mike Lischke
 *
 * See LICENSE file for more info.
 */

// cspell: disable

import {
    CharStream, CommonTokenStream, ParserRuleContext,
} from "antlr4ng";
import { describe, expect, it } from "vitest";

import { SQLiteLexer } from "./generated/SQLiteLexer";
import { SQLiteParser } from "./generated/SQLiteParser";
import { CodeCompletionCore } from "../src/CodeCompletionCore";

const createParser = (input: string): { parser: SQLiteParser; context: ParserRuleContext } => {
    const inputStream = CharStream.fromString(input);
    const lexer = new SQLiteLexer(inputStream);
    const tokenStream = new CommonTokenStream(lexer);
    const parser = new SQLiteParser(tokenStream);
    parser.removeErrorListeners();

    return { parser, context: parser.parse() };
};

describe("SQLite Code Completion Tests", () => {
    it("returns SQLite statements at the start of an input", () => {
        const { parser, context } = createParser("");
        const core = new CodeCompletionCore(parser);
        const candidates = core.collectCandidates(0, context);

        expect(candidates.tokens.size).toBe(25);
        expect(candidates.rules.size).toBe(0);
        expect(candidates.tokens.has(SQLiteLexer.SELECT_)).toBe(true);
        expect(candidates.tokens.has(SQLiteLexer.CREATE_)).toBe(true);
        expect(candidates.tokens.has(SQLiteLexer.INSERT_)).toBe(true);
        expect(candidates.tokens.has(SQLiteLexer.UPDATE_)).toBe(true);
    });

    it("returns clauses after FROM and ignores identifiers", () => {
        const { parser, context } = createParser("SELECT * FROM ");
        const core = new CodeCompletionCore(parser);
        core.ignoredTokens = new Set([SQLiteLexer.STAR, SQLiteLexer.IDENTIFIER]);

        const candidates = core.collectCandidates(3, context);

        expect(candidates.tokens.size).toBe(12);
        expect(candidates.rules.size).toBe(0);
        expect(candidates.tokens.has(SQLiteLexer.IDENTIFIER)).toBe(false);
        expect(candidates.tokens.has(SQLiteLexer.WHERE_)).toBe(true);
        expect(candidates.tokens.has(SQLiteLexer.ORDER_)).toBe(true);
        expect(candidates.tokens.has(SQLiteLexer.LIMIT_)).toBe(true);
    });

    it("returns expression candidates after WHERE and ignores identifiers", () => {
        const { parser, context } = createParser("SELECT * FROM users WHERE ");
        const core = new CodeCompletionCore(parser);
        core.ignoredTokens = new Set([SQLiteLexer.STAR, SQLiteLexer.IDENTIFIER]);

        const candidates = core.collectCandidates(5, context);

        expect(candidates.tokens.size).toBe(97);
        expect(candidates.rules.size).toBe(0);
        expect(candidates.tokens.has(SQLiteLexer.IDENTIFIER)).toBe(false);
        expect(candidates.tokens.has(SQLiteLexer.STRING_LITERAL)).toBe(true);
        expect(candidates.tokens.has(SQLiteLexer.OPEN_PAR)).toBe(true);
    });

    it("suggests schema and table rules after FROM", () => {
        const { parser, context } = createParser("SELECT * FROM ");
        const core = new CodeCompletionCore(parser);
        core.preferredRules = new Set([
            SQLiteParser.RULE_schema_name,
            SQLiteParser.RULE_table_name,
        ]);

        const candidates = core.collectCandidates(6, context);

        expect(candidates.tokens.size).toBe(98);
        expect(candidates.rules.size).toBe(2);
        expect(candidates.rules.has(SQLiteParser.RULE_schema_name)).toBe(true);
        expect(candidates.rules.has(SQLiteParser.RULE_table_name)).toBe(true);
        expect(candidates.rules.get(SQLiteParser.RULE_schema_name)?.startTokenIndex).toBe(6);
        expect(candidates.rules.get(SQLiteParser.RULE_table_name)?.startTokenIndex).toBe(6);
    });

    it("suggests a column alias rule after AS", () => {
        const { parser, context } = createParser("SELECT name AS ");
        const core = new CodeCompletionCore(parser);
        core.preferredRules = new Set([SQLiteParser.RULE_column_alias]);

        const candidates = core.collectCandidates(6, context);

        expect(candidates.tokens.size).toBe(0);
        expect(candidates.rules.size).toBe(1);
        expect(candidates.rules.has(SQLiteParser.RULE_column_alias)).toBe(true);
        expect(candidates.rules.get(SQLiteParser.RULE_column_alias)?.startTokenIndex).toBe(6);
    });

    it("suggests the next clause after a completed SELECT", () => {
        const { parser, context } = createParser("SELECT name FROM users ");
        const core = new CodeCompletionCore(parser);
        core.ignoredTokens = new Set([SQLiteLexer.IDENTIFIER]);

        const candidates = core.collectCandidates(4, context);

        expect(candidates.tokens.size).toBe(137);
        expect(candidates.rules.size).toBe(0);
        expect(candidates.tokens.has(SQLiteLexer.WHERE_)).toBe(true);
        expect(candidates.tokens.has(SQLiteLexer.GROUP_)).toBe(true);
        expect(candidates.tokens.has(SQLiteLexer.ORDER_)).toBe(true);
        expect(candidates.tokens.has(SQLiteLexer.LIMIT_)).toBe(true);
    });

    it("suggests rules in a complex CTE join query", () => {
        const input = "WITH recent AS (SELECT u.id, u.name, COUNT(o.id) AS order_count FROM main.users AS u "
            + "LEFT JOIN orders AS o ON o.user_id = u.id WHERE o.created_at >= '2025-01-01' GROUP BY u.id, u.name "
            + "HAVING COUNT(o.id) > 2) SELECT r.name, r.order_count FROM recent AS r JOIN audit AS a "
            + "ON a.user_id = r.id ORDER BY ";
        const { parser, context } = createParser(input);
        const core = new CodeCompletionCore(parser);
        core.preferredRules = new Set([
            SQLiteParser.RULE_schema_name,
            SQLiteParser.RULE_table_name,
            SQLiteParser.RULE_column_name,
            SQLiteParser.RULE_column_alias,
            SQLiteParser.RULE_function_name,
        ]);

        const candidates = core.collectCandidates(142, context);

        expect(candidates.tokens.size).toBe(108);
        expect(candidates.rules.size).toBe(3);
        for (const rule of [
            SQLiteParser.RULE_function_name,
            SQLiteParser.RULE_schema_name,
            SQLiteParser.RULE_table_name,
        ]) {
            expect(candidates.rules.has(rule)).toBe(true);
            expect(candidates.rules.get(rule)?.startTokenIndex).toBe(142);
        }
        expect(candidates.rules.has(SQLiteParser.RULE_column_name)).toBe(false);
        expect(candidates.rules.has(SQLiteParser.RULE_column_alias)).toBe(false);
    });

    it("suggests rules in a complex CTE subquery", () => {
        const input = "WITH totals AS (SELECT customer_id, SUM(amount) AS total FROM payments WHERE status = 'paid' "
            + "GROUP BY customer_id) SELECT c.name AS customer_name, t.total FROM customers AS c JOIN totals AS t "
            + "ON t.customer_id = c.id WHERE c.active = 1 GROUP BY c.name, t.total HAVING t.total > "
            + "(SELECT AVG(total) FROM totals) ORDER BY customer_name DESC LIMIT ";
        const { parser, context } = createParser(input);
        const core = new CodeCompletionCore(parser);
        core.preferredRules = new Set([
            SQLiteParser.RULE_schema_name,
            SQLiteParser.RULE_table_name,
            SQLiteParser.RULE_column_name,
            SQLiteParser.RULE_column_alias,
            SQLiteParser.RULE_function_name,
        ]);

        const candidates = core.collectCandidates(137, context);

        expect(candidates.tokens.size).toBe(108);
        expect(candidates.rules.size).toBe(3);
        for (const rule of [
            SQLiteParser.RULE_function_name,
            SQLiteParser.RULE_schema_name,
            SQLiteParser.RULE_table_name,
        ]) {
            expect(candidates.rules.has(rule)).toBe(true);
            expect(candidates.rules.get(rule)?.startTokenIndex).toBe(137);
        }
        expect(candidates.rules.has(SQLiteParser.RULE_column_name)).toBe(false);
        expect(candidates.rules.has(SQLiteParser.RULE_column_alias)).toBe(false);
    });
});
