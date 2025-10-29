import { CharStreams, CommonTokenStream } from 'antlr4ts';
import { TSqlLexer } from './generated/TSqlLexer';
import { TSqlParser } from './generated/TSqlParser';
import { CodeCompletionCore, CandidatesCollection } from "../src/CodeCompletionCore";

async function suggest(sql: string, caretOffset: number) {
    // Создаём поток символов
    const input = CharStreams.fromString(sql);
    const lexer = new TSqlLexer(input);
    const tokens = new CommonTokenStream(lexer);
    const parser = new TSqlParser(tokens);

    // Запускаем грамматический вход (entry-правило). Назовем его tsql_file
    parser.tsql_file();

    // Создаём движок автодополнения
    const core = new CodeCompletionCore(parser);

    // Можно настроить предпочтительные правила (если есть): например, если хотим получать предложения именно для идентификаторов таблиц/столбцов
    // core.preferredRules = new Set<number>([/* TSqlParser.RULE_table_name, … */]);

    // Получаем кандидатов по позиции (tokenIndex соответствующий caretOffset)
    // Нужно преобразовать caretOffset (символы) в токенIndex — для простоты можно брать токенStream.tokenIndex на последнем токене
    const tokenIndex = tokens.tokens.length > 0 ? tokens.tokens[tokens.tokens.length - 1].tokenIndex : 0;

    const candidates: CandidatesCollection = core.collectCandidates(tokenIndex);

    // Выводим предложения
    console.log('Tokens:');
    for (const [tokType, tokList] of candidates.tokens) {
        console.log(`  Token type ${tokType}: possible text =`, tokList.map(t => parser.vocabulary.getLiteralName(t) || parser.vocabulary.getSymbolicName(t)));
    }
    console.log('Rules:');
    for (const [ruleIndex, ruleInfo] of candidates.rules) {
        console.log(`  Rule ${parser.ruleNames[ruleIndex]}: startTokenIndex=${ruleInfo.startTokenIndex}, ruleList=${ruleInfo.ruleList}`);
    }

    return candidates;
}

// Пример использования:
const sql = 'SELECT * FROM ';
const caretPos = sql.length; // курсор в конце
suggest(sql, caretPos)
    .then(cand => console.log('Done'))
    .catch(err => console.error(err));
