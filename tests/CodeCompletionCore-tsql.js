"use strict";
var __awaiter = (this && this.__awaiter) || function (thisArg, _arguments, P, generator) {
    function adopt(value) { return value instanceof P ? value : new P(function (resolve) { resolve(value); }); }
    return new (P || (P = Promise))(function (resolve, reject) {
        function fulfilled(value) { try { step(generator.next(value)); } catch (e) { reject(e); } }
        function rejected(value) { try { step(generator["throw"](value)); } catch (e) { reject(e); } }
        function step(result) { result.done ? resolve(result.value) : adopt(result.value).then(fulfilled, rejected); }
        step((generator = generator.apply(thisArg, _arguments || [])).next());
    });
};
var __generator = (this && this.__generator) || function (thisArg, body) {
    var _ = { label: 0, sent: function() { if (t[0] & 1) throw t[1]; return t[1]; }, trys: [], ops: [] }, f, y, t, g = Object.create((typeof Iterator === "function" ? Iterator : Object).prototype);
    return g.next = verb(0), g["throw"] = verb(1), g["return"] = verb(2), typeof Symbol === "function" && (g[Symbol.iterator] = function() { return this; }), g;
    function verb(n) { return function (v) { return step([n, v]); }; }
    function step(op) {
        if (f) throw new TypeError("Generator is already executing.");
        while (g && (g = 0, op[0] && (_ = 0)), _) try {
            if (f = 1, y && (t = op[0] & 2 ? y["return"] : op[0] ? y["throw"] || ((t = y["return"]) && t.call(y), 0) : y.next) && !(t = t.call(y, op[1])).done) return t;
            if (y = 0, t) op = [op[0] & 2, t.value];
            switch (op[0]) {
                case 0: case 1: t = op; break;
                case 4: _.label++; return { value: op[1], done: false };
                case 5: _.label++; y = op[1]; op = [0]; continue;
                case 7: op = _.ops.pop(); _.trys.pop(); continue;
                default:
                    if (!(t = _.trys, t = t.length > 0 && t[t.length - 1]) && (op[0] === 6 || op[0] === 2)) { _ = 0; continue; }
                    if (op[0] === 3 && (!t || (op[1] > t[0] && op[1] < t[3]))) { _.label = op[1]; break; }
                    if (op[0] === 6 && _.label < t[1]) { _.label = t[1]; t = op; break; }
                    if (t && _.label < t[2]) { _.label = t[2]; _.ops.push(op); break; }
                    if (t[2]) _.ops.pop();
                    _.trys.pop(); continue;
            }
            op = body.call(thisArg, _);
        } catch (e) { op = [6, e]; y = 0; } finally { f = t = 0; }
        if (op[0] & 5) throw op[1]; return { value: op[0] ? op[1] : void 0, done: true };
    }
};
Object.defineProperty(exports, "__esModule", { value: true });
var antlr4ts_1 = require("antlr4ts");
var TSqlLexer_1 = require("./generated/TSqlLexer");
var TSqlParser_1 = require("./generated/TSqlParser");
var CodeCompletionCore_1 = require("../src/CodeCompletionCore");
function suggest(sql, caretOffset) {
    return __awaiter(this, void 0, void 0, function () {
        var input, lexer, tokens, parser, core, tokenIndex, candidates, _i, _a, _b, tokType, tokList, _c, _d, _e, ruleIndex, ruleInfo;
        return __generator(this, function (_f) {
            input = antlr4ts_1.CharStreams.fromString(sql);
            lexer = new TSqlLexer_1.TSqlLexer(input);
            tokens = new antlr4ts_1.CommonTokenStream(lexer);
            parser = new TSqlParser_1.TSqlParser(tokens);
            // Запускаем грамматический вход (entry-правило). Назовем его tsql_file
            parser.tsql_file();
            core = new CodeCompletionCore_1.CodeCompletionCore(parser);
            tokenIndex = tokens.tokens.length > 0 ? tokens.tokens[tokens.tokens.length - 1].tokenIndex : 0;
            candidates = core.collectCandidates(tokenIndex);
            // Выводим предложения
            console.log('Tokens:');
            for (_i = 0, _a = candidates.tokens; _i < _a.length; _i++) {
                _b = _a[_i], tokType = _b[0], tokList = _b[1];
                console.log("  Token type ".concat(tokType, ": possible text ="), tokList.map(function (t) { return parser.vocabulary.getLiteralName(t) || parser.vocabulary.getSymbolicName(t); }));
            }
            console.log('Rules:');
            for (_c = 0, _d = candidates.rules; _c < _d.length; _c++) {
                _e = _d[_c], ruleIndex = _e[0], ruleInfo = _e[1];
                console.log("  Rule ".concat(parser.ruleNames[ruleIndex], ": startTokenIndex=").concat(ruleInfo.startTokenIndex, ", ruleList=").concat(ruleInfo.ruleList));
            }
            return [2 /*return*/, candidates];
        });
    });
}
// Пример использования:
var sql = 'SELECT * FROM ';
var caretPos = sql.length; // курсор в конце
suggest(sql, caretPos)
    .then(function (cand) { return console.log('Done'); })
    .catch(function (err) { return console.error(err); });
