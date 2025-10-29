"use strict";
/*
 * Copyright (c) Mike Lischke. All rights reserved.
 * Licensed under the MIT License. See License.txt in the project root for license information.
 */
Object.defineProperty(exports, "__esModule", { value: true });
exports.CodeCompletionCore = exports.CandidatesCollection = void 0;
/* eslint-disable max-classes-per-file */
var antlr4ng_1 = require("antlr4ng");
var utils_js_1 = require("./utils.js");
/**
 * All the candidates which have been found. Tokens and rules are separated.
 * Token entries include a list of tokens that directly follow them (see also the "following" member in the
 * FollowSetWithPath class).
 * Rule entries include the index of the starting token within the evaluated rule, along with a call stack of rules
 * found during evaluation.
 */
var CandidatesCollection = /** @class */ (function () {
    function CandidatesCollection() {
        this.tokens = new Map();
        this.rules = new Map();
    }
    return CandidatesCollection;
}());
exports.CandidatesCollection = CandidatesCollection;
/**
 * A record for a follow set along with the path at which this set was found.
 * If there is only a single symbol in the interval set then we also collect and store tokens which follow
 * this symbol directly in its rule (i.e. there is no intermediate rule transition). Only single label transitions
 * are considered. This is useful if you have a chain of tokens which can be suggested as a whole, because there is
 * a fixed sequence in the grammar.
 */
var FollowSetWithPath = /** @class */ (function () {
    function FollowSetWithPath() {
        this.path = [];
        this.following = [];
    }
    return FollowSetWithPath;
}());
/**
 * A list of follow sets (for a given state number) + all of them combined for quick hit tests + whether they are
 * exhaustive (false if subsequent yet-unprocessed rules could add further tokens to the follow set, true otherwise).
 * This data is static in nature (because the used ATN states are part of a static struct: the ATN).
 * Hence it can be shared between all C3 instances, however it depends on the actual parser class (type).
 */
var FollowSetsHolder = /** @class */ (function () {
    function FollowSetsHolder() {
    }
    return FollowSetsHolder;
}());
/** The main class for doing the collection process. */
var CodeCompletionCore = /** @class */ (function () {
    function CodeCompletionCore(parser) {
        // Debugging options. Print human readable ATN state and other info.
        /** Not dependent on showDebugOutput. Prints the collected rules + tokens to terminal. */
        this.showResult = false;
        /** Enables printing ATN state info to terminal. */
        this.showDebugOutput = false;
        /** Only relevant when showDebugOutput is true. Enables transition printing for a state. */
        this.debugOutputWithTransitions = false;
        /** Also depends on showDebugOutput. Enables call stack printing for each rule recursion. */
        this.showRuleStack = false;
        /**
         * Specify if preferred rules should translated top-down (higher index rule returns first) or
         * bottom-up (lower index rule returns first).
         */
        this.translateRulesTopDown = false;
        this.tokenStartIndex = 0;
        this.statesProcessed = 0;
        /**
         * A mapping of rule index + token stream position to end token positions.
         * A rule which has been visited before with the same input position will always produce the same output positions.
         */
        this.shortcutMap = new Map();
        /** The collected candidates (rules and tokens). */
        this.candidates = new CandidatesCollection();
        this.parser = parser;
        this.atn = parser.atn;
        this.vocabulary = parser.vocabulary;
        this.ruleNames = parser.ruleNames;
        this.ignoredTokens = new Set();
        this.preferredRules = new Set();
    }
    /**
     * This is the main entry point. The caret token index specifies the token stream index for the token which
     * currently covers the caret (or any other position you want to get code completion candidates for).
     * Optionally you can pass in a parser rule context which limits the ATN walk to only that or called rules.
     * This can significantly speed up the retrieval process but might miss some candidates (if they are outside of
     * the given context).
     *
     * @param caretTokenIndex The index of the token at the caret position.
     * @param context An option parser rule context to limit the search space.
     * @returns The collection of completion candidates.
     */
    CodeCompletionCore.prototype.collectCandidates = function (caretTokenIndex, context) {
        var _a;
        this.shortcutMap.clear();
        this.candidates.rules.clear();
        this.candidates.tokens.clear();
        this.statesProcessed = 0;
        this.precedenceStack = [];
        this.tokenStartIndex = (context === null || context === void 0 ? void 0 : context.start) ? context.start.tokenIndex : 0;
        // eslint-disable-next-line no-underscore-dangle
        var tokenStream = this.parser.tokenStream;
        this.tokens = [];
        var offset = this.tokenStartIndex;
        while (true) {
            var token = tokenStream.get(offset++);
            if (!token) {
                break;
            }
            if (token.channel === antlr4ng_1.Token.DEFAULT_CHANNEL) {
                this.tokens.push(token);
                if (token.tokenIndex >= caretTokenIndex || token.type === antlr4ng_1.Token.EOF) {
                    break;
                }
            }
            // Do not check for the token index here, as we want to end with the first unhidden token on or after
            // the caret.
            if (token.type === antlr4ng_1.Token.EOF) {
                break;
            }
        }
        var callStack = [];
        var startRule = context ? context.ruleIndex : 0;
        this.processRule(this.atn.ruleToStartState[startRule], 0, callStack, 0, 0);
        if (this.showResult) {
            console.log("States processed: ".concat(this.statesProcessed));
            console.log("\n\nCollected rules:\n");
            for (var _i = 0, _b = this.candidates.rules; _i < _b.length; _i++) {
                var rule = _b[_i];
                var path = "";
                for (var _c = 0, _d = rule[1].ruleList; _c < _d.length; _c++) {
                    var token = _d[_c];
                    path += this.ruleNames[token] + " ";
                }
                console.log(this.ruleNames[rule[0]] + ", path: ", path);
            }
            var sortedTokens = new Set();
            for (var _e = 0, _f = this.candidates.tokens; _e < _f.length; _e++) {
                var token = _f[_e];
                var value = (_a = this.vocabulary.getDisplayName(token[0])) !== null && _a !== void 0 ? _a : "";
                for (var _g = 0, _h = token[1]; _g < _h.length; _g++) {
                    var following = _h[_g];
                    value += " " + this.vocabulary.getDisplayName(following);
                }
                sortedTokens.add(value);
            }
            console.log("\n\nCollected tokens:\n");
            for (var _j = 0, sortedTokens_1 = sortedTokens; _j < sortedTokens_1.length; _j++) {
                var symbol = sortedTokens_1[_j];
                console.log(symbol);
            }
            console.log("\n\n");
        }
        return this.candidates;
    };
    /**
     * Checks if the predicate associated with the given transition evaluates to true.
     *
     * @param transition The transition to check.
     * @returns the evaluation result of the predicate.
     */
    CodeCompletionCore.prototype.checkPredicate = function (transition) {
        return transition.getPredicate().evaluate(this.parser, antlr4ng_1.ParserRuleContext.empty);
    };
    /**
     * Walks the rule chain upwards or downwards (depending on translateRulesTopDown) to see if that matches any of the
     * preferred rules. If found, that rule is added to the collection candidates and true is returned.
     *
     * @param ruleWithStartTokenList The list to convert.
     * @returns true if any of the stack entries was converted.
     */
    CodeCompletionCore.prototype.translateStackToRuleIndex = function (ruleWithStartTokenList) {
        if (this.preferredRules.size === 0) {
            return false;
        }
        // Change the direction we iterate over the rule stack
        if (this.translateRulesTopDown) {
            // Loop over the rule stack from lowest to highest rule level. This will prioritize a lower preferred rule
            // if it is a child of a higher one that is also a preferred rule.
            for (var i = ruleWithStartTokenList.length - 1; i >= 0; i--) {
                if (this.translateToRuleIndex(i, ruleWithStartTokenList)) {
                    return true;
                }
            }
        }
        else {
            // Loop over the rule stack from highest to lowest rule level. This will prioritize a higher preferred rule
            // if it contains a lower one that is also a preferred rule.
            for (var i = 0; i < ruleWithStartTokenList.length; i++) {
                if (this.translateToRuleIndex(i, ruleWithStartTokenList)) {
                    return true;
                }
            }
        }
        return false;
    };
    /**
     * Given the index of a rule from a rule chain, check if that matches any of the preferred rules. If it matches,
     * that rule is added to the collection candidates and true is returned.
     *
     * @param i The rule index.
     * @param ruleWithStartTokenList The list to check.
     * @returns true if the specified rule is in the list of preferred rules.
     */
    CodeCompletionCore.prototype.translateToRuleIndex = function (i, ruleWithStartTokenList) {
        var _a = ruleWithStartTokenList[i], ruleIndex = _a.ruleIndex, startTokenIndex = _a.startTokenIndex;
        if (this.preferredRules.has(ruleIndex)) {
            // Add the rule to our candidates list along with the current rule path,
            // but only if there isn't already an entry like that.
            var path = ruleWithStartTokenList.slice(0, i).map(function (_a) {
                var candidate = _a.ruleIndex;
                return candidate;
            });
            var addNew = true;
            var _loop_1 = function (rule) {
                if (rule[0] !== ruleIndex || rule[1].ruleList.length !== path.length) {
                    return "continue";
                }
                // Found an entry for this rule. Same path? If so don't add a new (duplicate) entry.
                if (path.every(function (v, j) { return v === rule[1].ruleList[j]; })) {
                    addNew = false;
                    return "break";
                }
            };
            for (var _i = 0, _b = this.candidates.rules; _i < _b.length; _i++) {
                var rule = _b[_i];
                var state_1 = _loop_1(rule);
                if (state_1 === "break")
                    break;
            }
            if (addNew) {
                this.candidates.rules.set(ruleIndex, {
                    startTokenIndex: startTokenIndex,
                    ruleList: path,
                });
                if (this.showDebugOutput) {
                    console.log("=====> collected: ", this.ruleNames[ruleIndex]);
                }
            }
            return true;
        }
        return false;
    };
    /**
     * This method follows the given transition and collects all symbols within the same rule that directly follow it
     * without intermediate transitions to other rules and only if there is a single symbol for a transition.
     *
     * @param transition The transition from which to start.
     * @returns A list of toke types.
     */
    CodeCompletionCore.prototype.getFollowingTokens = function (transition) {
        var _this = this;
        var result = [];
        var pipeline = [transition.target];
        while (pipeline.length > 0) {
            var state = pipeline.pop();
            if (state) {
                state.transitions.forEach(function (outgoing) {
                    if (outgoing.transitionType === antlr4ng_1.Transition.ATOM) {
                        if (!outgoing.isEpsilon) {
                            var list = outgoing.label.toArray();
                            if (list.length === 1 && !_this.ignoredTokens.has(list[0])) {
                                result.push(list[0]);
                                pipeline.push(outgoing.target);
                            }
                        }
                        else {
                            pipeline.push(outgoing.target);
                        }
                    }
                });
            }
        }
        return result;
    };
    /**
     * Entry point for the recursive follow set collection function.
     *
     * @param start Start state.
     * @param stop Stop state.
     * @returns Follow sets.
     */
    CodeCompletionCore.prototype.determineFollowSets = function (start, stop) {
        var sets = [];
        var stateStack = [];
        var ruleStack = [];
        var isExhaustive = this.collectFollowSets(start, stop, sets, stateStack, ruleStack);
        // Sets are split by path to allow translating them to preferred rules. But for quick hit tests
        // it is also useful to have a set with all symbols combined.
        var combined = new antlr4ng_1.IntervalSet();
        for (var _i = 0, sets_1 = sets; _i < sets_1.length; _i++) {
            var set = sets_1[_i];
            combined.addSet(set.intervals);
        }
        return { sets: sets, isExhaustive: isExhaustive, combined: combined };
    };
    /**
     * Collects possible tokens which could be matched following the given ATN state. This is essentially the same
     * algorithm as used in the LL1Analyzer class, but here we consider predicates also and use no parser rule context.
     *
     * @param s The state to continue from.
     * @param stopState The state which ends the collection routine.
     * @param followSets A pass through parameter to add found sets to.
     * @param stateStack A stack to avoid endless recursions.
     * @param ruleStack The current rule stack.
     * @returns true if the follow sets is exhaustive, i.e. we terminated before the rule end was reached, so no
     * subsequent rules could add tokens
     */
    CodeCompletionCore.prototype.collectFollowSets = function (s, stopState, followSets, stateStack, ruleStack) {
        if (stateStack.find(function (x) { return x === s; })) {
            return true;
        }
        stateStack.push(s);
        if (s === stopState || s.constructor.stateType === antlr4ng_1.ATNState.RULE_STOP) {
            stateStack.pop();
            return false;
        }
        var isExhaustive = true;
        for (var _i = 0, _a = s.transitions; _i < _a.length; _i++) {
            var transition = _a[_i];
            if (transition.transitionType === antlr4ng_1.Transition.RULE) {
                var ruleTransition = transition;
                if (ruleStack.indexOf(ruleTransition.target.ruleIndex) !== -1) {
                    continue;
                }
                ruleStack.push(ruleTransition.target.ruleIndex);
                var ruleFollowSetsIsExhaustive = this.collectFollowSets(transition.target, stopState, followSets, stateStack, ruleStack);
                ruleStack.pop();
                // If the subrule had an epsilon transition to the rule end, the tokens added to
                // the follow set are non-exhaustive and we should continue processing subsequent transitions post-rule
                if (!ruleFollowSetsIsExhaustive) {
                    var nextStateFollowSetsIsExhaustive = this.collectFollowSets(ruleTransition.followState, stopState, followSets, stateStack, ruleStack);
                    isExhaustive && (isExhaustive = nextStateFollowSetsIsExhaustive);
                }
            }
            else if (transition.transitionType === antlr4ng_1.Transition.PREDICATE) {
                if (this.checkPredicate(transition)) {
                    var nextStateFollowSetsIsExhaustive = this.collectFollowSets(transition.target, stopState, followSets, stateStack, ruleStack);
                    isExhaustive && (isExhaustive = nextStateFollowSetsIsExhaustive);
                }
            }
            else if (transition.isEpsilon) {
                var nextStateFollowSetsIsExhaustive = this.collectFollowSets(transition.target, stopState, followSets, stateStack, ruleStack);
                isExhaustive && (isExhaustive = nextStateFollowSetsIsExhaustive);
            }
            else if (transition.transitionType === antlr4ng_1.Transition.WILDCARD) {
                var set = new FollowSetWithPath();
                set.intervals = antlr4ng_1.IntervalSet.of(antlr4ng_1.Token.MIN_USER_TOKEN_TYPE, this.atn.maxTokenType);
                set.path = ruleStack.slice();
                followSets.push(set);
            }
            else {
                var label = transition.label;
                if (label && label.length > 0) {
                    if (transition.transitionType === antlr4ng_1.Transition.NOT_SET) {
                        label = label.complement(antlr4ng_1.Token.MIN_USER_TOKEN_TYPE, this.atn.maxTokenType);
                    }
                    var set = new FollowSetWithPath();
                    set.intervals = label !== null && label !== void 0 ? label : new antlr4ng_1.IntervalSet();
                    set.path = ruleStack.slice();
                    set.following = this.getFollowingTokens(transition);
                    followSets.push(set);
                }
            }
        }
        stateStack.pop();
        return isExhaustive;
    };
    /**
     * Walks the ATN for a single rule only. It returns the token stream position for each path that could be matched
     * in this rule.
     * The result can be empty in case we hit only non-epsilon transitions that didn't match the current input or if we
     * hit the caret position.
     *
     * @param startState The start state.
     * @param tokenListIndex The token index we are currently at.
     * @param callStack The stack that indicates where in the ATN we are currently.
     * @param precedence The current precedence level.
     * @param indentation A value to determine the current indentation when doing debug prints.
     * @returns the set of token stream indexes (which depend on the ways that had to be taken).
     */
    CodeCompletionCore.prototype.processRule = function (startState, tokenListIndex, callStack, precedence, indentation) {
        // Start with rule specific handling before going into the ATN walk.
        // Check first if we've taken this path with the same input before.
        var positionMap = this.shortcutMap.get(startState.ruleIndex);
        if (!positionMap) {
            positionMap = new Map();
            this.shortcutMap.set(startState.ruleIndex, positionMap);
        }
        else {
            if (positionMap.has(tokenListIndex)) {
                if (this.showDebugOutput) {
                    console.log("=====> shortcut");
                }
                return positionMap.get(tokenListIndex);
            }
        }
        var result = new Set();
        // For rule start states we determine and cache the follow set, which gives us 3 advantages:
        // 1) We can quickly check if a symbol would be matched when we follow that rule. We can so check in advance
        //    and can save us all the intermediate steps if there is no match.
        // 2) We'll have all symbols that are collectable already together when we are at the caret on rule enter.
        // 3) We get this lookup for free with any 2nd or further visit of the same rule, which often happens
        //    in non trivial grammars, especially with (recursive) expressions and of course when invoking code
        //    completion multiple times.
        var setsPerState = CodeCompletionCore.followSetsByATN.get(this.parser.constructor.name);
        if (!setsPerState) {
            setsPerState = new Map();
            CodeCompletionCore.followSetsByATN.set(this.parser.constructor.name, setsPerState);
        }
        var followSets = setsPerState.get(startState.stateNumber);
        if (!followSets) {
            var stop_1 = this.atn.ruleToStopState[startState.ruleIndex];
            followSets = this.determineFollowSets(startState, stop_1);
            setsPerState.set(startState.stateNumber, followSets);
        }
        // Get the token index where our rule starts from our (possibly filtered) token list
        var startTokenIndex = this.tokens[tokenListIndex].tokenIndex;
        callStack.push({
            startTokenIndex: startTokenIndex,
            ruleIndex: startState.ruleIndex,
        });
        if (tokenListIndex >= this.tokens.length - 1) { // At caret?
            if (this.preferredRules.has(startState.ruleIndex)) {
                // No need to go deeper when collecting entries and we reach a rule that we want to collect anyway.
                this.translateStackToRuleIndex(callStack);
            }
            else {
                // Convert all follow sets to either single symbols or their associated preferred rule and add
                // the result to our candidates list.
                for (var _i = 0, _a = followSets.sets; _i < _a.length; _i++) {
                    var set = _a[_i];
                    var fullPath = callStack.slice();
                    // Rules derived from our followSet will always start at the same token as our current rule.
                    var followSetPath = set.path.map(function (path) {
                        return {
                            startTokenIndex: startTokenIndex,
                            ruleIndex: path,
                        };
                    });
                    fullPath.push.apply(fullPath, followSetPath);
                    if (!this.translateStackToRuleIndex(fullPath)) {
                        for (var _b = 0, _c = set.intervals.toArray(); _b < _c.length; _b++) {
                            var symbol = _c[_b];
                            if (!this.ignoredTokens.has(symbol)) {
                                if (this.showDebugOutput) {
                                    console.log("=====> collected: ", this.vocabulary.getDisplayName(symbol));
                                }
                                if (!this.candidates.tokens.has(symbol)) {
                                    // Following is empty if there is more than one entry in the set.
                                    this.candidates.tokens.set(symbol, set.following);
                                }
                                else {
                                    // More than one following list for the same symbol.
                                    if (this.candidates.tokens.get(symbol) !== set.following) {
                                        this.candidates.tokens.set(symbol, []);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            if (!followSets.isExhaustive) {
                // If we're at the caret but the follow sets is non-exhaustive (empty or all tokens are optional),
                // we should continue to collect tokens following this rule
                result.add(tokenListIndex);
            }
            callStack.pop();
            return result;
        }
        else {
            // Process the rule if we either could pass it without consuming anything (epsilon transition)
            // or if the current input symbol will be matched somewhere after this entry point.
            // Otherwise stop here.
            var currentSymbol = this.tokens[tokenListIndex].type;
            if (followSets.isExhaustive && !followSets.combined.contains(currentSymbol)) {
                callStack.pop();
                return result;
            }
        }
        if (startState.isLeftRecursiveRule) {
            this.precedenceStack.push(precedence);
        }
        // The current state execution pipeline contains all yet-to-be-processed ATN states in this rule.
        // For each such state we store the token index + a list of rules that lead to it.
        var statePipeline = [];
        var currentEntry;
        // Bootstrap the pipeline.
        statePipeline.push({ state: startState, tokenListIndex: tokenListIndex });
        while (statePipeline.length > 0) {
            currentEntry = statePipeline.pop();
            ++this.statesProcessed;
            var currentSymbol = this.tokens[currentEntry.tokenListIndex].type;
            var atCaret = currentEntry.tokenListIndex >= this.tokens.length - 1;
            if (this.showDebugOutput) {
                this.printDescription(indentation, currentEntry.state, this.generateBaseDescription(currentEntry.state), currentEntry.tokenListIndex);
                if (this.showRuleStack) {
                    this.printRuleState(callStack);
                }
            }
            if (currentEntry.state.constructor.stateType === antlr4ng_1.ATNState.RULE_STOP) {
                // Record the token index we are at, to report it to the caller.
                result.add(currentEntry.tokenListIndex);
                continue;
            }
            var transitions = currentEntry.state.transitions;
            // We simulate here the same precedence handling as the parser does, which uses hard coded values.
            // For rules that are not left recursive this value is ignored (since there is no precedence transition).
            for (var _d = 0, transitions_1 = transitions; _d < transitions_1.length; _d++) {
                var transition = transitions_1[_d];
                switch (transition.transitionType) {
                    case antlr4ng_1.Transition.RULE: {
                        var ruleTransition = transition;
                        var endStatus = this.processRule(transition.target, currentEntry.tokenListIndex, callStack, ruleTransition.precedence, indentation + 1);
                        for (var _e = 0, endStatus_1 = endStatus; _e < endStatus_1.length; _e++) {
                            var position = endStatus_1[_e];
                            statePipeline.push({
                                state: transition.followState,
                                tokenListIndex: position,
                            });
                        }
                        break;
                    }
                    case antlr4ng_1.Transition.PREDICATE: {
                        if (this.checkPredicate(transition)) {
                            statePipeline.push({
                                state: transition.target,
                                tokenListIndex: currentEntry.tokenListIndex,
                            });
                        }
                        break;
                    }
                    case antlr4ng_1.Transition.PRECEDENCE: {
                        var predTransition = transition;
                        if (predTransition.precedence >= this.precedenceStack[this.precedenceStack.length - 1]) {
                            statePipeline.push({
                                state: transition.target,
                                tokenListIndex: currentEntry.tokenListIndex,
                            });
                        }
                        break;
                    }
                    case antlr4ng_1.Transition.WILDCARD: {
                        if (atCaret) {
                            if (!this.translateStackToRuleIndex(callStack)) {
                                for (var _f = 0, _g = antlr4ng_1.IntervalSet.of(antlr4ng_1.Token.MIN_USER_TOKEN_TYPE, this.atn.maxTokenType)
                                    .toArray(); _f < _g.length; _f++) {
                                    var token = _g[_f];
                                    if (!this.ignoredTokens.has(token)) {
                                        this.candidates.tokens.set(token, []);
                                    }
                                }
                            }
                        }
                        else {
                            statePipeline.push({
                                state: transition.target,
                                tokenListIndex: currentEntry.tokenListIndex + 1,
                            });
                        }
                        break;
                    }
                    default: {
                        if (transition.isEpsilon) {
                            // Jump over simple states with a single outgoing epsilon transition.
                            statePipeline.push({
                                state: transition.target,
                                tokenListIndex: currentEntry.tokenListIndex,
                            });
                            continue;
                        }
                        var set = transition.label;
                        if (set && set.length > 0) {
                            if (transition.transitionType === antlr4ng_1.Transition.NOT_SET) {
                                set = set.complement(antlr4ng_1.Token.MIN_USER_TOKEN_TYPE, this.atn.maxTokenType);
                            }
                            if (atCaret) {
                                if (!this.translateStackToRuleIndex(callStack)) {
                                    var list = set.toArray();
                                    var hasTokenSequence = list.length === 1;
                                    for (var _h = 0, list_1 = list; _h < list_1.length; _h++) {
                                        var symbol = list_1[_h];
                                        if (!this.ignoredTokens.has(symbol)) {
                                            if (this.showDebugOutput) {
                                                console.log("=====> collected: ", this.vocabulary.getDisplayName(symbol));
                                            }
                                            var followingTokens = hasTokenSequence
                                                ? this.getFollowingTokens(transition)
                                                : [];
                                            if (!this.candidates.tokens.has(symbol)) {
                                                this.candidates.tokens.set(symbol, followingTokens);
                                            }
                                            else {
                                                this.candidates.tokens.set(symbol, (0, utils_js_1.longestCommonPrefix)(followingTokens, this.candidates.tokens.get(symbol)));
                                            }
                                        }
                                    }
                                }
                            }
                            else if (set.contains(currentSymbol)) {
                                if (this.showDebugOutput) {
                                    console.log("=====> consumed: ", this.vocabulary.getDisplayName(currentSymbol));
                                }
                                statePipeline.push({
                                    state: transition.target,
                                    tokenListIndex: currentEntry.tokenListIndex + 1,
                                });
                            }
                        }
                    }
                }
            }
        }
        callStack.pop();
        if (startState.isLeftRecursiveRule) {
            this.precedenceStack.pop();
        }
        // Cache the result, for later lookup to avoid duplicate walks.
        positionMap.set(tokenListIndex, result);
        return result;
    };
    CodeCompletionCore.prototype.generateBaseDescription = function (state) {
        var stateValue = state.stateNumber === antlr4ng_1.ATNState.INVALID_STATE_NUMBER ? "Invalid" : state.stateNumber;
        var typeName = CodeCompletionCore.atnStateTypeMap[state.constructor.stateType];
        return "[".concat(stateValue, " ").concat(typeName, "] in ").concat(this.ruleNames[state.ruleIndex]);
    };
    CodeCompletionCore.prototype.printDescription = function (indentation, state, baseDescription, tokenIndex) {
        var indent = "  ".repeat(indentation);
        var output = indent;
        var transitionDescription = "";
        if (this.debugOutputWithTransitions) {
            for (var _i = 0, _a = state.transitions; _i < _a.length; _i++) {
                var transition = _a[_i];
                var labels = "";
                var symbols = transition.label ? transition.label.toArray() : [];
                if (symbols.length > 2) {
                    // Only print start and end symbols to avoid large lists in debug output.
                    labels = this.vocabulary.getDisplayName(symbols[0]) + " .. " +
                        this.vocabulary.getDisplayName(symbols[symbols.length - 1]);
                }
                else {
                    for (var _b = 0, symbols_1 = symbols; _b < symbols_1.length; _b++) {
                        var symbol = symbols_1[_b];
                        if (labels.length > 0) {
                            labels += ", ";
                        }
                        labels += this.vocabulary.getDisplayName(symbol);
                    }
                }
                if (labels.length === 0) {
                    labels = "ε";
                }
                var typeName = CodeCompletionCore.atnStateTypeMap[transition.target.constructor.stateType];
                transitionDescription += "\n".concat(indent, "\t(").concat(labels, ") [").concat(transition.target.stateNumber, " ") +
                    "".concat(typeName, "] in ").concat(this.ruleNames[transition.target.ruleIndex]);
            }
        }
        if (tokenIndex >= this.tokens.length - 1) {
            output += "<<".concat(this.tokenStartIndex + tokenIndex, ">> ");
        }
        else {
            output += "<".concat(this.tokenStartIndex + tokenIndex, "> ");
        }
        console.log(output + "Current state: " + baseDescription + transitionDescription);
    };
    CodeCompletionCore.prototype.printRuleState = function (stack) {
        if (stack.length === 0) {
            console.log("<empty stack>");
            return;
        }
        for (var _i = 0, stack_1 = stack; _i < stack_1.length; _i++) {
            var rule = stack_1[_i];
            console.log(this.ruleNames[rule.ruleIndex]);
        }
    };
    CodeCompletionCore.followSetsByATN = new Map();
    CodeCompletionCore.atnStateTypeMap = [
        "invalid",
        "basic",
        "rule start",
        "block start",
        "plus block start",
        "star block start",
        "token start",
        "rule stop",
        "block end",
        "star loop back",
        "star loop entry",
        "plus loop back",
        "loop end",
    ];
    return CodeCompletionCore;
}());
exports.CodeCompletionCore = CodeCompletionCore;
