using UnityEngine;

// The imported rig plays authored skeletal clips; this root supplies a bounded lab trajectory.
public class NeurologicalDog : MonoBehaviour
{
    [SerializeField] private Animator _animator;
    [SerializeField] private Transform _shadow;
    private float _floorY;
    private Vector3 _origin;
    private Quaternion _orientation=Quaternion.identity;
    private bool _captured;
    private float _time;
    public NeurologicalScenario Scenario {get;private set;}
    public void Configure(Animator animator,Transform shadow=null){_animator=animator;_shadow=shadow;}
    private void Awake()=>CapturePose();
    private void CapturePose(){if(_captured)return;_origin=transform.localPosition;_orientation=transform.localRotation;_floorY=transform.position.y;_captured=true;}
    public void Play(NeurologicalScenario scenario)
    {
        CapturePose();Scenario=scenario;_time=0;gameObject.SetActive(true);
        if(_animator){_animator.applyRootMotion=false;_animator.Play(scenario==NeurologicalScenario.ReviewRequired?"ReviewIdle":scenario==NeurologicalScenario.Normal?"NormalWalk":scenario.ToString(),0,0);_animator.Update(0);}
        Animate(0);
    }
    public void Stop(){CapturePose();gameObject.SetActive(false);transform.localPosition=_origin;transform.localRotation=_orientation;}
    private void Update(){_time+=Time.deltaTime;Animate(_time);}
    public void Animate(float time)
    {
        Vector3 offset=Vector3.zero;Quaternion facing=Quaternion.identity;
        if(Scenario==NeurologicalScenario.CircleLeft||Scenario==NeurologicalScenario.CircleRight)
        {
            float sign=Scenario==NeurologicalScenario.CircleLeft?-1:1;float angle=time*.8f*sign;
            offset=new Vector3(Mathf.Sin(angle)*.36f,0,Mathf.Cos(angle)*.36f);
            facing=Quaternion.LookRotation(new Vector3(Mathf.Cos(angle)*sign,0,-Mathf.Sin(angle)*sign));
        }
        else if(Scenario!=NeurologicalScenario.ReviewRequired)
        {
            // A short straight walk followed by a deliberate turn, rather than compulsive circling.
            float phase=Mathf.Repeat(time,12);float z=phase<5?Mathf.Lerp(-.35f,.35f,phase/5):phase<6?.35f:phase<11?Mathf.Lerp(.35f,-.35f,(phase-6)/5):-.35f;
            float yaw=phase<5?0:phase<6?Mathf.SmoothStep(0,180,phase-5):phase<11?180:Mathf.SmoothStep(180,360,phase-11);
            offset.z=z;facing=Quaternion.Euler(0,yaw,0);
            if(Scenario==NeurologicalScenario.CerebellarAtaxia){offset.x=Mathf.Sin(time*2.3f)*.045f;offset.y=Mathf.Abs(Mathf.Sin(time*5))*.012f;facing*=Quaternion.Euler(0,0,Mathf.Sin(time*3.1f)*4);}
            if(Scenario==NeurologicalScenario.VestibularAtaxia){offset.x=Mathf.Sin(time*1.8f)*.075f;facing*=Quaternion.Euler(0,0,6+Mathf.Sin(time*2)*3);}
        }
        transform.localPosition=_origin+_orientation*offset;transform.localRotation=_orientation*facing;
        if(_shadow){_shadow.position=new Vector3(transform.position.x,_floorY+.012f,transform.position.z);_shadow.rotation=Quaternion.Euler(90,transform.eulerAngles.y,0);}
    }
}
