import json,sys
p=r"c:\Users\biges\Desktop\BusBuddy\FETCHABILITY-INDEX.json"
try:
    with open(p,'r',encoding='utf-8') as f:
        s=f.read()
    json.loads(s)
    print('VALID')
except json.JSONDecodeError as e:
    print('INVALID')
    print(f'Line: {e.lineno}, Col: {e.colno}, Msg: {e.msg}')
    # show context
    lines=s.splitlines()
    for i in range(max(0,e.lineno-3), min(len(lines), e.lineno+2)):
        print(f'{i+1:6}: {lines[i]})')
    sys.exit(1)
except Exception as e:
    print('ERROR')
    print(e)
    sys.exit(2)
