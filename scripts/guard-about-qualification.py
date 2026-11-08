import os,re,subprocess

def validate(env,actual):
    expected=env.get("EXPECTED_SHA", "")
    if not re.fullmatch("[0-9a-f]{40}",expected):raise ValueError("Exact reviewed full SHA required")
    if env.get("GITHUB_EVENT_NAME")!="workflow_dispatch" or env.get("GITHUB_RUN_ATTEMPT")!="1":raise ValueError("Dispatch first attempt only")
    if env.get("GITHUB_REPOSITORY")!="frpboy/Scrobblix":raise ValueError("Repository mismatch")
    if actual!=expected or env.get("GITHUB_SHA")!=expected:raise ValueError("Reviewed SHA, checkout and event must match")

if __name__=="__main__":
    validate(os.environ,subprocess.check_output(["git","rev-parse","HEAD"],text=True).strip())
    print("PASS exact reviewed SHA/checkout/event and attempt-one guard")
