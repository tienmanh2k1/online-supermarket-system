"""Test script to properly reproduce the 2 bugs in prepare-migration-script.py"""
import sys
import re

# Copy tokenize_sql from prepare-migration-script.py
def tokenize_sql(sql_text):
    tokens = []
    i = 0
    n = len(sql_text)
    code_start = 0

    while i < n:
        if sql_text[i] == "'":
            if i > code_start:
                tokens.append(('code', sql_text[code_start:i]))
            str_start = i
            i += 1
            while i < n:
                if sql_text[i] == "'":
                    if i + 1 < n and sql_text[i + 1] == "'":
                        i += 2
                    else:
                        i += 1
                        break
                elif sql_text[i] == '\\':
                    i += 2
                else:
                    i += 1
            tokens.append(('string', sql_text[str_start:i]))
            code_start = i
        elif sql_text[i] == '"':
            if i > code_start:
                tokens.append(('code', sql_text[code_start:i]))
            str_start = i
            i += 1
            while i < n:
                if sql_text[i] == '"':
                    if i + 1 < n and sql_text[i + 1] == '"':
                        i += 2
                    else:
                        i += 1
                        break
                elif sql_text[i] == '\\':
                    i += 2
                else:
                    i += 1
            tokens.append(('string_dq', sql_text[str_start:i]))
            code_start = i
        elif sql_text[i] == '`':
            if i > code_start:
                tokens.append(('code', sql_text[code_start:i]))
            str_start = i
            i += 1
            while i < n:
                if sql_text[i] == '`':
                    if i + 1 < n and sql_text[i + 1] == '`':
                        i += 2
                    else:
                        i += 1
                        break
                else:
                    i += 1
            tokens.append(('identifier', sql_text[str_start:i]))
            code_start = i
        elif sql_text[i:i+2] == '--':
            if i > code_start:
                tokens.append(('code', sql_text[code_start:i]))
            cmt_start = i
            i = sql_text.find('\n', i)
            if i == -1:
                i = n
            else:
                i += 1
            tokens.append(('comment_line', sql_text[cmt_start:i]))
            code_start = i
        elif sql_text[i:i+2] == '/*':
            if i > code_start:
                tokens.append(('code', sql_text[code_start:i]))
            cmt_start = i
            end_idx = sql_text.find('*/', i + 2)
            if end_idx == -1:
                i = n
            else:
                i = end_idx + 2
            tokens.append(('comment_block', sql_text[cmt_start:i]))
            code_start = i
        else:
            i += 1

    if code_start < n:
        tokens.append(('code', sql_text[code_start:n]))

    return tokens


def process_END_real(tokens, verbose=True):
    """Replicates the exact logic from prepare-migration-script.py lines 296-378"""
    all_proc_tokens = tokens

    sub_pieces = []
    tokens_consumed = 0
    block_stack = []
    found_first_begin = False
    terminal_end_found = False
    remaining_in_token = ""

    kw_regex = re.compile(
        r'(?<![`\w])(BEGIN|CASE|END\s+IF|END\s+CASE|END\s+WHILE|END\s+LOOP|END\s+REPEAT|END)(?![`\w])',
        re.IGNORECASE
    )

    for p_tok_type, p_tok_val in all_proc_tokens:
        tokens_consumed += 1
        if verbose:
            print(f"\n--- Processing token {tokens_consumed}: type={p_tok_type}, val={repr(p_tok_val[:50])}...")
        if p_tok_type != 'code':
            sub_pieces.append(p_tok_val)
            continue

        words = list(kw_regex.finditer(p_tok_val))
        last_w_end = 0

        for w in words:
            kw = re.sub(r'\s+', ' ', w.group(1).upper())
            w_start = w.start()
            w_end = w.end()

            if kw == 'BEGIN':
                block_stack.append('BEGIN')
                found_first_begin = True
                if verbose:
                    print(f"  -> Found BEGIN, block_stack={block_stack}")
            elif kw == 'CASE':
                block_stack.append('CASE')
            elif kw == 'END CASE':
                block_stack.pop()
            elif kw in ('END IF', 'END WHILE', 'END LOOP', 'END REPEAT'):
                pass
            elif kw == 'END':
                if verbose:
                    print(f"  -> Found END at pos {w_start}-{w_end}, block_stack={block_stack}")
                if not block_stack:
                    raise ValueError("Unexpected END without matching block")

                if block_stack[-1] == 'CASE':
                    block_stack.pop()
                    if verbose:
                        print(f"     Closed CASE, block_stack={block_stack}")
                elif block_stack[-1] == 'BEGIN':
                    tail = p_tok_val[w_end:]
                    end_semi_match = re.match(r'(\s*(?:`?\w+`?\s*)?)(;;|;)', tail)
                    if verbose:
                        print(f"     tail={repr(tail)}, end_semi_match={end_semi_match}")
                    if end_semi_match:
                        if found_first_begin and len(block_stack) == 1:
                            block_stack.pop()
                            label_and_semi = end_semi_match.group(0)
                            matched_label = end_semi_match.group(1)
                            if verbose:
                                print(f"     *** TERMINAL END FOUND *** (len=1, label={repr(matched_label)})")
                            sub_pieces.append(p_tok_val[last_w_end:w_start])
                            sub_pieces.append(p_tok_val[w_start:w_end] + matched_label + ';;')
                            after_end_pos = w_end + len(label_and_semi)
                            remaining_in_token = p_tok_val[after_end_pos:]
                            last_w_end = after_end_pos
                            terminal_end_found = True
                            break
                        else:
                            if verbose:
                                print(f"     Inner BEGIN closed (len={len(block_stack)}), pop and continue")
                            block_stack.pop()
                            sub_pieces.append(p_tok_val[last_w_end:])
                    else:
                        # Look ahead
                        if verbose:
                            print(f"     No semicolon in tail, looking ahead...")
                        remaining_tokens = all_proc_tokens[tokens_consumed:]
                        label_in_next_token = None
                        semi_in_next_token = False
                        look_ahead_idx = None

                        for _look_ahead_idx, (look_tok_type, look_tok_val) in enumerate(remaining_tokens):
                            if look_tok_type in ('comment_line', 'comment_block') or look_tok_val.isspace():
                                continue
                            if look_tok_type == 'identifier':
                                label_in_next_token = look_tok_val
                                look_ahead_idx = _look_ahead_idx
                                next_idx = look_ahead_idx + 1
                                if next_idx < len(remaining_tokens):
                                    next_tok_type, next_tok_val = remaining_tokens[next_idx]
                                    if next_tok_type == 'code' and (';' in next_tok_val or ';;' in next_tok_val):
                                        semi_in_next_token = True
                                break
                            elif look_tok_type == 'code':
                                if ';' in look_tok_val or ';;' in look_tok_val:
                                    semi_in_next_token = True
                                look_ahead_idx = _look_ahead_idx
                                break
                            else:
                                look_ahead_idx = _look_ahead_idx
                                break

                        if label_in_next_token and semi_in_next_token:
                            label_tok_idx = look_ahead_idx
                            semi_tok_idx = look_ahead_idx + 1
                            label_tok_val = remaining_tokens[label_tok_idx][1]
                            semi_tok_val = remaining_tokens[semi_tok_idx][1]

                            if found_first_begin and len(block_stack) == 1 and block_stack[-1] == 'BEGIN':
                                if verbose:
                                    print(f"     *** TERMINAL END FOUND (look-ahead) *** (len=1, label={repr(label_in_next_token)})")
                                block_stack.pop()
                                sub_pieces.append(p_tok_val[last_w_end:w_start])
                                sub_pieces.append(p_tok_val[w_start:w_end] + label_in_next_token + ';;')

                                semi_marker = ';;' if ';;' in semi_tok_val else ';'
                                semi_pos = semi_tok_val.find(semi_marker) + len(semi_marker)
                                remaining_in_token = semi_tok_val[semi_pos:]
                                tokens_consumed += 1 + label_tok_idx + 1
                                if verbose:
                                    print(f"     tokens_consumed += {1 + label_tok_idx + 1}")
                                terminal_end_found = True
                                break
                            else:
                                if verbose:
                                    print(f"     Inner BEGIN closed (look-ahead, len={len(block_stack)}), pop and continue")
                                block_stack.pop()
                                sub_pieces.append(p_tok_val[last_w_end:])
                        else:
                            raise ValueError("BEGIN block END not followed by semicolon")

        if terminal_end_found:
            if verbose:
                print(f"\n*** Breaking outer loop, terminal_end_found=True ***")
            break

    if verbose:
        print(f"\nFinal: terminal_end_found={terminal_end_found}, block_stack={block_stack}")
        print(f"sub_pieces: {sub_pieces}")
        print(f"remaining_in_token: {repr(remaining_in_token)}")
        print(f"tokens_consumed: {tokens_consumed}")

    return terminal_end_found, sub_pieces, remaining_in_token, tokens_consumed


def test_case_1():
    """Bug 1: Nested BEGIN with label in next token"""
    print("\n" + "="*70)
    print("TEST 1: Nested BEGIN - label in next token")
    print("="*70)

    # When END is followed by a label in a SEPARATE token
    sql = "CREATE PROCEDURE p()\nBEGIN\n  BEGIN SELECT 1;\nEND label;\n  SELECT 2;\nEND;"

    tokens = tokenize_sql(sql)
    print("\nTokens:")
    for i, tok in enumerate(tokens):
        print(f"  {i}: {tok}")

    print("\nProcessing:")
    try:
        result = process_END_real(tokens)
    except ValueError as e:
        print(f"\nException: {e}")


def test_case_2():
    """Bug 2: Label output duplicated"""
    print("\n" + "="*70)
    print("TEST 2: Label with comment before it")
    print("="*70)

    # When there's a comment between END and label
    sql = "CREATE PROCEDURE p()\nBEGIN\n  SELECT 1;\nEND -- comment\n`label`;"

    tokens = tokenize_sql(sql)
    print("\nTokens:")
    for i, tok in enumerate(tokens):
        print(f"  {i}: {tok}")

    print("\nProcessing:")
    try:
        result = process_END_real(tokens)
    except ValueError as e:
        print(f"\nException: {e}")


def test_case_3():
    """Bug 1 variant: Nested BEGIN without label - should work"""
    print("\n" + "="*70)
    print("TEST 3: Nested BEGIN without label (should work)")
    print("="*70)

    sql = "CREATE PROCEDURE p()\nBEGIN\n  BEGIN SELECT 1; END;\n  SELECT 2;\nEND;"

    tokens = tokenize_sql(sql)
    print("\nTokens:")
    for i, tok in enumerate(tokens):
        print(f"  {i}: {tok}")

    print("\nProcessing:")
    try:
        result = process_END_real(tokens)
    except ValueError as e:
        print(f"\nException: {e}")


def test_case_4():
    """Bug 2 variant: Simple backtick label"""
    print("\n" + "="*70)
    print("TEST 4: Simple backtick label (END and label in separate tokens)")
    print("="*70)

    sql = "END\n`outer_block`;"

    tokens = tokenize_sql(sql)
    print("\nTokens:")
    for i, tok in enumerate(tokens):
        print(f"  {i}: {tok}")

    print("\nSimulating look-ahead logic:")
    print("  remaining_tokens = all_proc_tokens[1:]")
    print("  look_ahead_idx=0: ('identifier', '`outer_block`')")
    print("  look_ahead_idx=1: ('code', ';')")
    print("  label_in_next_token = '`outer_block`'")
    print("  semi_in_next_token = True")
    print()
    print("  label_tok_idx = 0")
    print("  semi_tok_idx = 1")
    print("  tokens_consumed += 1 + 0 + 1 = 2")
    print()
    print("  But actual tokens consumed should be: 2 (END token) + 1 (identifier) + 1 (semicolon) = 4")
    print("  Wait, tokens_consumed starts at 0 and increments at start of loop...")
    print("  After processing END token: tokens_consumed = 1")
    print("  After look-ahead: tokens_consumed += 2 = 3")
    print("  Remaining tokens after loop: [identifier, semicolon] - SHOULD BE CONSUMED!")
    print()
    print("  BUG: The label token and semicolon token are NOT removed from the token stream!")


if __name__ == "__main__":
    test_case_1()
    test_case_2()
    test_case_3()
    test_case_4()
